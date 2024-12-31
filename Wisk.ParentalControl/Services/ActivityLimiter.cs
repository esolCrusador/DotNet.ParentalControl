using Microsoft.Extensions.Options;
using System.Diagnostics;
using Wisk.ParentalControl;

namespace ParentalControlPoc.Services
{
    public class ActivityLimiter
    {
        private readonly ActivityMonitor _activityMonitor;
        private readonly ILogger _logger;
        private readonly MonitorConfiguration _options;

        private Dictionary<string, ProcessActivityNotification>? _current;
        private IDisposable? _subscription;

        public ActivityLimiter(ActivityMonitor activityMonitor, IOptions<MonitorConfiguration> options, ILogger<ActivityMonitor> logger)
        {
            _activityMonitor = activityMonitor;
            _logger = logger;
            _options = options.Value;
        }
        public void Start()
        {
            _subscription = _activityMonitor.SpentTime.Subscribe(processesActivity =>
            {
                _current ??= processesActivity.ToDictionary(pan => pan.ProcessName);

                foreach (var processActivity in processesActivity)
                    HandleActivity(processActivity);
            });
        }
        private void HandleActivity(ProcessActivityNotification processActivity)
        {
            if (!processActivity.IsActive)
                return;

            var limit = GetLimit(processActivity.ProcessName);
            if (!limit.HasValue)
                return;

            if (processActivity.SpentToday > limit.Value)
            {
                foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processActivity.ProcessName)))
                {
                    process.Kill();
                    _logger.LogInformation($"Stopped {processActivity.ProcessName} (Process ID: {process.Id})");
                    MessageBox.Show($"Time limit {limit.Value} exceeded for {processActivity.ProcessName}");
                }
                return;
            }

            var timeLeft = limit.Value - processActivity.SpentToday;
            var previousActivity = _current!.GetValueOrDefault(processActivity.ProcessName);
            var previousActivityTimeLeft = limit.Value - previousActivity?.SpentToday ?? processActivity.SpentToday;

            foreach (var notificationsInterval in _options.NotificationIntervals.OrderByDescending(i => i))
            {
                if (timeLeft < notificationsInterval && (previousActivity == null || previousActivity == processActivity || timeLeft > previousActivityTimeLeft))
                {
                    MessageBox.Show($"Left {timeLeft} for {processActivity.ProcessName}");
                    break;
                }
            }

            _current![processActivity.ProcessName] = processActivity;
        }
        public void Stop()
        {
            _subscription?.Dispose();
        }

        private TimeSpan? GetLimit(string processName)
        {
            var appLimits = _options.AppLimits.GetValueOrDefault(processName);
            if (appLimits == null)
                return null;

            if (appLimits.DateLimits.TryGetValue(DateTime.Today, out var dateLimit))
                return dateLimit;

            if (appLimits.DayLimit.TryGetValue(DateTime.Today.DayOfWeek, out var dayLimit))
                return dayLimit;

            return appLimits.Default;
        }
    }
}
