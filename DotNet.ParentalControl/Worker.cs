using DotNet.ParentalControl.Services;

namespace DotNet.ParentalControl
{
    public class Worker : IHostedService
    {
        private readonly ActivityMonitor _activityMonitor;
        private readonly ActivityLimiter _activityLimiter;
        private readonly ILogger<Worker> _logger;

        public Worker(ActivityMonitor activityMonitor, ActivityLimiter activityLimiter, ILogger<Worker> logger)
        {
            _activityMonitor = activityMonitor;
            _activityLimiter = activityLimiter;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _activityMonitor.Start();
            _activityLimiter.Start();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _activityMonitor.Stop();
            _activityLimiter.Stop();

            return Task.CompletedTask;
        }
    }
}
