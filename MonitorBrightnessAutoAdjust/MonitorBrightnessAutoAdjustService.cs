using Microsoft.Extensions.Logging;
using MonitorBrightnessAutoAdjust.Sensors;
using Monitorian.Core.Models.Monitor;
using Monitorian.Core.Models.Watcher;
using System.Collections.ObjectModel;
using System.Windows.Forms;

namespace MonitorBrightnessAutoAdjust
{
    public sealed class MonitorBrightnessAutoAdjustService
    {
        public static EventHandler<Tuple<double, int>> OnEnvironmentLightChanged;

        private readonly ILogger _logger;

        private readonly TSL2591 _lightSensor;
        private double _latestLux = 0d;
        private int _latestBrightnessLevel = 0;

        private readonly SessionWatcher _sessionWatcher;
        private readonly PowerWatcher _powerWatcher;
        private readonly DisplaySettingsWatcher _displaySettingsWatcher;

        public MonitorBrightnessAutoAdjustService(ILogger<MonitorBrightnessAutoAdjustBackgroundService> logger)
        {
            _logger = logger;

            //
            Monitors = new ObservableCollection<IMonitor>();

            _sessionWatcher = new SessionWatcher();
            _powerWatcher = new PowerWatcher();
            _displaySettingsWatcher = new DisplaySettingsWatcher();

            _sessionWatcher.Subscribe((e) => OnMonitorsChangeInferred(nameof(SessionWatcher), e));
            _powerWatcher.Subscribe((e) => OnMonitorsChangeInferred(nameof(PowerWatcher), e));
            _displaySettingsWatcher.Subscribe((e) => OnMonitorsChangeInferred(nameof(DisplaySettingsWatcher), e));

            //
            _lightSensor = new TSL2591();
        }

        #region Monitors

        private async void OnMonitorsChangeInferred(object sender, ICountEventArgs e = null, bool force = false)
        {
            await ProceedScanAsync(e);
        }

        public async Task ProceedScanAsync(ICountEventArgs e, bool force = false)
        {
            await ScanAsync(TimeSpan.FromSeconds(3));

            // set brightness by lux again, preview set action may failure or new monitor may not set
            AutoAdjust(force);
        }


        public ObservableCollection<IMonitor> Monitors { get; }
        private readonly object _monitorsLock = new();

        internal event EventHandler<bool> ScanningChanged;

        private IMonitor GetMonitor(IMonitor monitorItem) => monitorItem;
        private void DisposeMonitor(IMonitor monitor) => monitor?.Dispose();


        private int _scanCount = 0;
        private int _updateCount = 0;

        internal Task ScanAsync() => ScanAsync(TimeSpan.Zero);

        private async Task ScanAsync(TimeSpan interval)
        {
            var isEntered = false;
            try
            {
                isEntered = (Interlocked.Increment(ref _scanCount) == 1);
                if (isEntered)
                {
                    ScanningChanged?.Invoke(this, true);

                    var intervalTask = (interval > TimeSpan.Zero) ? Task.Delay(interval) : Task.CompletedTask;

                    await Task.Run(async () =>
                    {
                        var oldMonitorIndices = Enumerable.Range(0, Monitors.Count).ToList();
                        var newMonitorItems = new List<IMonitor>();

                        foreach (var item in await MonitorManager.EnumerateMonitorsAsync(TimeSpan.FromSeconds(12)))
                        {
                            var oldMonitorExists = false;

                            foreach (int index in oldMonitorIndices)
                            {
                                var oldMonitor = Monitors[index];
                                var oldMonitorUpdate = oldMonitor.UpdateBrightness();
                                if (string.Equals(oldMonitor.DeviceInstanceId, item.DeviceInstanceId, StringComparison.OrdinalIgnoreCase)
                                    && oldMonitorUpdate.Status == AccessStatus.Succeeded)
                                {
                                    oldMonitorExists = true;
                                    oldMonitorIndices.Remove(index);
                                    oldMonitor = item;
                                    break;
                                }
                            }

                            if (!oldMonitorExists)
                                newMonitorItems.Add(item);
                        }

                        if (oldMonitorIndices.Count > 0)
                        {
                            oldMonitorIndices.Reverse(); // Reverse indices to start removing from the tail.
                            foreach (int index in oldMonitorIndices)
                            {
                                DisposeMonitor(Monitors[index]);
                                lock (_monitorsLock)
                                {
                                    Monitors.RemoveAt(index);
                                }
                            }
                        }

                        if (newMonitorItems.Count > 0)
                        {
                            foreach (var item in newMonitorItems)
                            {
                                var newMonitor = GetMonitor(item);
                                lock (_monitorsLock)
                                {
                                    Monitors.Add(newMonitor);
                                }
                            }
                        }
                    });

                    await intervalTask;
                }
            }
            finally
            {
                if (isEntered)
                {
                    ScanningChanged?.Invoke(this, false);

                    Interlocked.Exchange(ref _scanCount, 0);
                }
            }
        }
        #endregion Monitors

        private DateTime _latestReInitializeTime = DateTime.Now;

        public double GetLux()
        {
            var lux = _lightSensor.GetLux();

            switch (lux)
            {
                case 0:
                    {
                        // may light sensor can't access
                        var deviceId = _lightSensor.GetId();
                        if (deviceId == 0)
                        {
                            _logger.LogInformation($"Light sensor can't access...");

                            // may compute sleep or usb reconnected, try re-initialize
                            // retry initialize interval larger than 2 min
                            if ((DateTime.Now - _latestReInitializeTime).TotalMinutes > 2)
                            {
                                _logger.LogInformation($"Light sensor can't access, retry re-initialize...");
                                _lightSensor.Initialize();
                                lux = _lightSensor.GetLux();
                                _latestReInitializeTime = DateTime.Now;
                            }
                        }

                        break;
                    }
                case < 0:
                    lux = 0;
                    break;
            }

            return lux;
        }

        private int _autoAdjustCount = 0;

        public int AutoAdjust(bool force = false)
        {
            var isEntered = false;
            try
            {
                isEntered = (Interlocked.Increment(ref _autoAdjustCount) == 1);
                if (isEntered)
                {
                    var lux = GetLux();
                    _logger.LogInformation($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{lux}");

                    if (force || Math.Abs(lux - _latestLux) > 2)
                    {
                        var brightnessLevel = ComputeMonitorBrightnessLevel(lux);
                        if (force || brightnessLevel != _latestBrightnessLevel)
                        {
                            var setResultMap = SetMonitorBrightness(Monitors, brightnessLevel);
                            _logger.LogInformation(
                                $"Lux change: {_latestLux}->{lux}, monitor brightness change: {_latestBrightnessLevel}->{brightnessLevel}...");

                            _latestBrightnessLevel = brightnessLevel;
                        }

                        _latestLux = lux;

                        OnEnvironmentLightChanged?.Invoke(this,
                            new Tuple<double, int>(_latestLux, _latestBrightnessLevel));
                    }
                }
                return _latestBrightnessLevel;
            }
            finally
            {
                if (isEntered)
                {
                    Interlocked.Exchange(ref _autoAdjustCount, 0);
                }
            }
        }

        private static List<Tuple<double, double, int>> EnviromentLuxBrightnessLevelPredefineTable =
        [
            new(-1, 10, 0),
            new(10, 20, 24),
            new(20, 40, 32),
            new(40, 100, 46),
            new(100, 200, 58),
            new(200, 400, 64),
            new(400, 1200, 73),
            new(1200, 2000, 86),
            new(2000, double.MaxValue, 100),
        ];

        private int ComputeMonitorBrightnessLevel(double lux)
        {
            var rangeIndex = EnviromentLuxBrightnessLevelPredefineTable.FindIndex(t => lux > t.Item1 && lux <= t.Item2);
            var currentRange = EnviromentLuxBrightnessLevelPredefineTable[rangeIndex];
            if (rangeIndex == EnviromentLuxBrightnessLevelPredefineTable.Count - 1)
            {
                // max brightness, NOT need linear compute
                return currentRange.Item3;
            }

            var rangeMinLevel = currentRange.Item3;
            var rangeMaxLevel = EnviromentLuxBrightnessLevelPredefineTable[
                rangeIndex == EnviromentLuxBrightnessLevelPredefineTable.Count - 1
                    ? rangeIndex
                    : rangeIndex + 1].Item3;
            var level = currentRange.Item3 + (int)((rangeMaxLevel - rangeMinLevel) / (currentRange.Item2 - currentRange.Item1) *
                                                   (lux - currentRange.Item1));
            return level;
        }

        private Dictionary<IMonitor, AccessResult> SetMonitorBrightness(IEnumerable<IMonitor> monitors, int brightnessLevel)
        {
            var setResultList = new Dictionary<IMonitor, AccessResult>();
            foreach (var monitor in monitors)
            {
                var result = monitor.SetBrightness(brightnessLevel);
                setResultList.Add(monitor, result);
                _logger.LogInformation($"{monitor.Description} brightness changed to: {brightnessLevel}, {result.Status}");
            }
            return setResultList;
        }
    }
}
