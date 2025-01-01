using Microsoft.Extensions.Logging.EventLog;
using ParentalControlPoc.Services;

namespace Wisk.ParentalControl
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateDefaultBuilder(args).UseWindowsService();
            builder.ConfigureServices((ctx, services) =>
            {
                services.AddLogging(lb => lb.AddEventLog(new EventLogSettings
                {
                    SourceName = "ParetnalControl",
                    LogName = "ParentalControl"
                }).AddConsole());
                services.AddHostedService<Worker>();
                services.Configure<MonitorConfiguration>(option =>
                {
                    ctx.Configuration.GetSection("Monitor").Bind(option, b => b.ErrorOnUnknownConfiguration = true);
                });
                services.AddSingleton<ActivityMonitor>();
                services.AddSingleton<ActivityLimiter>();
            });

            var host = builder.Build();
            host.Run();
        }
    }
}