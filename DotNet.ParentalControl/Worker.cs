using DotNet.ParentalControl.Configuration;
using DotNet.ParentalControl.Services;
using System.Reactive.Linq;

namespace DotNet.ParentalControl
{
    public class Worker : IHostedService
    {
        private readonly ActivityMonitor _activityMonitor;
        private readonly ActivityLimiter _activityLimiter;
        private readonly MonitorConfiguration _monitorConfiguration;
        private readonly ILogger<Worker> _logger;
        private IDisposable? _refreshConfiguration;

        public Worker(ActivityMonitor activityMonitor, ActivityLimiter activityLimiter, MonitorConfiguration monitorConfiguration, ILogger<Worker> logger)
        {
            _activityMonitor = activityMonitor;
            _activityLimiter = activityLimiter;
            _monitorConfiguration = monitorConfiguration;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Start(cancellationToken);

            _refreshConfiguration = _monitorConfiguration.Changed.Select(_ =>
            Observable.FromAsync(async cancellation =>
                {
                    await Stop(cancellation);
                    await Start(cancellation);

                    _logger.LogInformation("Configuration reloaded");
                })
            ).Concat().Subscribe();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Stop(cancellationToken);
            _refreshConfiguration?.Dispose();
        }

        private Task Start(CancellationToken cancellationToken)
        {
            _activityMonitor.Start();
            _activityLimiter.Start();

            return Task.CompletedTask;
        }

        private Task Stop(CancellationToken cancellationToken)
        {
            _activityMonitor.Stop();
            _activityLimiter.Stop();

            return Task.CompletedTask;
        }
    }
}
