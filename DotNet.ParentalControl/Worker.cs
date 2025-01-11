using DotNet.ParentalControl.Configuration;
using DotNet.ParentalControl.Services;

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

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _activityMonitor.Start();
            _activityLimiter.Start();
            _refreshConfiguration = _monitorConfiguration.Changed.Subscribe(_ =>
            {
                _activityMonitor.Stop();
                _activityLimiter.Stop();

                _activityMonitor.Start();
                _activityLimiter.Start();

                _logger.LogInformation("Configuration reloaded");
            });

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _activityMonitor.Stop();
            _activityLimiter.Stop();
            _refreshConfiguration?.Dispose();

            return Task.CompletedTask;
        }
    }
}
