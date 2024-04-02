using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;

namespace MonitorBrightnessAutoAdjust;

internal static class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static async Task Main()
    {
        ApplicationConfiguration.Initialize();

        string[] args = Environment.GetCommandLineArgs().Skip(1).ToArray();

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        // TODO: add file logger
        builder.Services.AddSingleton<MonitorBrightnessAutoAdjustService>();
        builder.Services.AddHostedService<MonitorBrightnessAutoAdjustBackgroundService>();
        builder.Services.AddSingleton<AmbientLightApplicationContext>();

        LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(builder.Services);

        builder.Services.Configure<AmbientLightApplicationContext>(builder.Configuration);

        using var host = builder.Build();
        await host.StartAsync();

        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

        using (var scope = host.Services.CreateScope())
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var ambientLightApplicationContext = scope.ServiceProvider.GetRequiredService<AmbientLightApplicationContext>();
            Application.Run(ambientLightApplicationContext);
        }

        lifetime.StopApplication();
        await host.WaitForShutdownAsync();
    }
}