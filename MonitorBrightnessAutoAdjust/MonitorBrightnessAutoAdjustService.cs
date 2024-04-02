using Microsoft.Extensions.Logging;
using MonitorBrightnessAutoAdjust.Sensors;
using Monitorian.Core.Models.Monitor;

namespace MonitorBrightnessAutoAdjust
{
    public sealed class MonitorBrightnessAutoAdjustService
    {
        public static EventHandler<Tuple<double, int>> OnEnvironmentLightChanged;

        private readonly ILogger _logger;

        private List<IMonitor> _monitors;
        private readonly TSL2591 _lightSensor;
        private double _latestLux = 0d;
        private int _latestBrightnessLevel = 0;

        public MonitorBrightnessAutoAdjustService(ILogger<MonitorBrightnessAutoAdjustBackgroundService> logger)
        {
            _logger = logger;

            //
            InitializeMonitors();

            //
            _lightSensor = new TSL2591();
        }

        private void InitializeMonitors()
        {
            var monitorsTask = MonitorManager.EnumerateMonitorsAsync(TimeSpan.FromSeconds(10));
            monitorsTask.ConfigureAwait(false);
            monitorsTask.Wait(TimeSpan.FromSeconds(10));
            _monitors = monitorsTask.Result.OrderBy(t => t.DeviceInstanceId).ToList();
            foreach (var mb in _monitors)
            {
                _logger.LogInformation($"Monitor: {mb.DisplayIndex} {mb.DeviceInstanceId} {mb.Description} {mb.IsBrightnessSupported} {mb.UpdateBrightness().Status} {mb.Brightness}");
            }
        }

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

        private DateTime _latestReInitilizeMonitorTime = DateTime.Now;

        public int AutoAdjust()
        {
            var lux = GetLux();
            _logger.LogInformation($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{lux}");

            if (Math.Abs(lux - _latestLux) > 2)
            {
                var brightnessLevel = ComputeMonitorBrightnessLevel(lux);
                if (brightnessLevel != _latestBrightnessLevel)
                {
                    _logger.LogInformation($"Lux change: {_latestLux}->{lux}, monitor brightness change: {_latestBrightnessLevel}->{brightnessLevel}...");

                    var setResultMap = SetMonitorBrightness(_monitors,brightnessLevel);
                    if (setResultMap.Any(t => t.Value != AccessResult.Succeeded))
                    {
                        _logger.LogInformation($"Monitor {setResultMap.FirstOrDefault(t=>t.Value != AccessResult.Succeeded).Key.Description} set failure...");

                        // some monitor set failure, retry get monitors then set brightness level
                        // retry initialize interval larger than 2 min
                        if ((DateTime.Now - _latestReInitilizeMonitorTime).TotalMinutes > 2)
                        {
                            _logger.LogInformation($"Some monitor set failure, retry re-initialize...");

                            InitializeMonitors();
                            setResultMap = SetMonitorBrightness(_monitors, brightnessLevel);
                            _logger.LogInformation($"Lux change: {_latestLux}->{lux}, monitor brightness change: {_latestBrightnessLevel}->{brightnessLevel}...");
                        } 
                    }

                    _latestBrightnessLevel = brightnessLevel;
                }

                _latestLux = lux;

                if (OnEnvironmentLightChanged != null)
                {
                    OnEnvironmentLightChanged(this, new Tuple<double, int>(_latestLux, _latestBrightnessLevel));
                }
            }

            return _latestBrightnessLevel;
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
