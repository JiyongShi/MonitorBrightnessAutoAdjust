using Microsoft.Extensions.Logging;
using MonitorBrightnessAutoAdjust.Sensors;
using Monitorian.Core.Models.Monitor;

namespace MonitorBrightnessAutoAdjust
{
    public sealed class MonitorBrightnessAutoAdjustService
    {
        public static EventHandler<Tuple<double, int>> OnEnvironmentLightChanged;

        private readonly ILogger _logger;

        private readonly List<IMonitor> _monitors;
        private readonly TSL2591 _lightSensor;
        private double _latestLux = 0d;
        private int _latestLuxLevel = 0;

        public MonitorBrightnessAutoAdjustService(ILogger<MonitorBrightnessAutoAdjustBackgroundService> logger)
        {
            _logger = logger;

            //
            var monitorsTask = MonitorManager.EnumerateMonitorsAsync(TimeSpan.FromSeconds(10));
            monitorsTask.ConfigureAwait(false);
            monitorsTask.Wait(TimeSpan.FromSeconds(10));
            _monitors = monitorsTask.Result.OrderBy(t => t.DeviceInstanceId).ToList();
            foreach (var mb in _monitors)
            {
                _logger.LogInformation($"Monitor: {mb.DisplayIndex} {mb.DeviceInstanceId} {mb.Description} {mb.IsBrightnessSupported} {mb.UpdateBrightness().Status} {mb.Brightness}");
            }

            //
            _lightSensor = new TSL2591();
        }

        public double GetLux()
        {
            var lux = _lightSensor.GetLux();
            if (lux < 0)
            {
                lux = 0;
            }

            return lux;
        }

        public int AutoAdjust()
        {
            var lux = GetLux();
            _logger.LogInformation($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{lux}");

            if (Math.Abs(lux - _latestLux) > 2)
            {
                var luxLevel = ComputeMonitorBrightnessLevel(lux);
                if (luxLevel != _latestLuxLevel)
                {
                    _logger.LogInformation($"Lux change: {_latestLux}->{lux}, monitor brightness change: {_latestLuxLevel}->{luxLevel}...");

                    SetMonitorBrightnessByEnvironmentLux(luxLevel, _monitors);
                    _latestLuxLevel = luxLevel;
                }

                _latestLux = lux;

                if (OnEnvironmentLightChanged != null)
                {
                    OnEnvironmentLightChanged(this, new Tuple<double, int>(_latestLux, _latestLuxLevel));
                }
            }

            return _latestLuxLevel;
        }

        private static List<Tuple<double, double, int>> EnviromentLuxBrightnessPercentPredefineTable =
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
            var rangeIndex = EnviromentLuxBrightnessPercentPredefineTable.FindIndex(t => lux > t.Item1 && lux <= t.Item2);
            var currentRange = EnviromentLuxBrightnessPercentPredefineTable[rangeIndex];
            if (rangeIndex == EnviromentLuxBrightnessPercentPredefineTable.Count - 1)
            {
                // max brightness, NOT need linear compute
                return currentRange.Item3;
            }

            var rangeMinLevel = currentRange.Item3;
            var rangeMaxLevel = EnviromentLuxBrightnessPercentPredefineTable[
                rangeIndex == EnviromentLuxBrightnessPercentPredefineTable.Count - 1
                    ? rangeIndex
                    : rangeIndex + 1].Item3;
            var level = currentRange.Item3 + (int)((rangeMaxLevel - rangeMinLevel) / (currentRange.Item2 - currentRange.Item1) *
                                                   (lux - currentRange.Item1));
            return level;
        }

        private int SetMonitorBrightnessByEnvironmentLux(int level, IEnumerable<IMonitor> monitors)
        {
            SetMonitorBrightness(monitors, level);
            return level;
        }

        private void SetMonitorBrightness(IEnumerable<IMonitor> monitors, int brightnessPercent)
        {
            foreach (var monitor in monitors)
            {
                var result = monitor.SetBrightness(brightnessPercent);
                _logger.LogInformation($"{monitor.Description} brightness changed to: {brightnessPercent}, {result.Status}");
            }
        }
    }
}
