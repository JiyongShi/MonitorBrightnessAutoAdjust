﻿using Microsoft.Extensions.Logging;
using Monitorian.Core.Models.Watcher;

namespace MonitorBrightnessAutoAdjust
{
    /// <summary>
    /// notify icon app context
    /// </summary>
    internal sealed class AmbientLightApplicationContext : ApplicationContext
    {
        private ILogger<AmbientLightApplicationContext> _logger;

        private NotifyIcon _notifyIcon;
        private MonitorBrightnessAutoAdjustService _monitorBrightnessAutoAdjustService;

        public AmbientLightApplicationContext(ILogger<AmbientLightApplicationContext> logger, MonitorBrightnessAutoAdjustService brightnessAutoAdjustService)
        {
            _logger = logger;
            _monitorBrightnessAutoAdjustService = brightnessAutoAdjustService;

            MonitorBrightnessAutoAdjustService.OnEnvironmentLightChanged += OnEnvironmentLightChanged;
            _notifyIcon = new NotifyIcon()
            {
                // default icon, if light sensor connected and can access light lux, will show lux value.
                Icon = Resources.AmbientLight,
                ContextMenuStrip = new ContextMenuStrip()
                {
                    Items =
                    {
                        new ToolStripMenuItem("AutoStart", null, AutoStart)
                        {
                            Checked = AutoStartUtil.IsAutoRun(AutoRunRegPath, AutoRunName),
                        },
                        new ToolStripMenuItem("Refresh", null, Refresh),
                        new ToolStripMenuItem("Exit", null, Exit),
                    }
                },
                Text = @"When light sensor connected, will show lux value.",
                Visible = true
            };

            Refresh(this, EventArgs.Empty);
        }

        private void Refresh(object? sender, EventArgs e)
        {
            var scanTask = Task.Run(async () =>
            {
                await _monitorBrightnessAutoAdjustService.ProceedScanAsync(new CountEventArgs(0), true);
            });
            scanTask.ConfigureAwait(false);
        }


        public const string AutoRunRegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string AutoRunName = "MonitorBrightnessAutoAdjust";

        private void AutoStart(object? sender, EventArgs e)
        {
            var toolStripMenuItem = sender as ToolStripMenuItem;
            if (toolStripMenuItem == null)
            {
                return;
            }

            AutoStartUtil.SetAutoRun(AutoRunRegPath, AutoRunName, !toolStripMenuItem.Checked);
            toolStripMenuItem.Checked = !toolStripMenuItem.Checked;
        }

        private void OnEnvironmentLightChanged(object? sender, Tuple<double, int> e)
        {
            _notifyIcon.Icon = LightIconGenerator.GenerateIcon((int)e.Item1);
            _notifyIcon.Text = $@"Light({e.Item1:####}), Brightness({e.Item2}%)";
        }

        void Exit(object? sender, EventArgs e)
        {
            _notifyIcon.Visible = false;
            Application.Exit();
        }
    }
}
