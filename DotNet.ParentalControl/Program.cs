using DotNet.ParentalControl.Configuration;
using DotNet.ParentalControl.Services;
using Microsoft.Extensions.Logging.EventLog;
using Serilog;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DotNet.ParentalControl
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateDefaultBuilder(args).UseWindowsService();
            builder.ConfigureServices((ctx, services) =>
            {
                services.AddLogging(lb =>
                {
                    lb.AddEventLog(new EventLogSettings
                    {
                        SourceName = "ParetnalControl",
                        LogName = "ParentalControl"
                    });
                    lb.AddConsole();
                    lb.AddSerilog(new LoggerConfiguration().ReadFrom.Configuration(ctx.Configuration).CreateLogger(), true);
                });
                services.AddHostedService<Worker>();
                services.Configure<MonitorOptions>(option =>
                {
                    ctx.Configuration.GetSection("Monitor").Bind(option, b => b.ErrorOnUnknownConfiguration = true);
                });
                services.AddSingleton(new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                services.AddSingleton<ActivityMonitor>();
                services.AddSingleton<ActivityLimiter>();
                services.AddSingleton<MonitorConfiguration>();
            });

            var host = builder.Build();
            var _ = ShutDownAfter(host);

            host.Run();
        }

        private static async Task ShutDownAfter(IHost host)
        {
            var shutDownAfter = host.Services.GetRequiredService<IConfiguration>().GetValue<TimeSpan?>("ShutDownAfter");
            if (!shutDownAfter.HasValue)
                return;

            await Task.Delay(shutDownAfter.Value);
            await host.StopAsync(default);
        }
    }
}