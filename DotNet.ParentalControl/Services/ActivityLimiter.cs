using DotNet.ParentalControl.Configuration;
using DotNet.ParentalControl.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DotNet.ParentalControl.Services
{
    public class ActivityLimiter
    {
        private readonly ActivityMonitor _activityMonitor;
        private readonly ILogger _logger;
        private readonly MonitorConfiguration _configuration;
        private readonly ConcurrentDictionary<string, ProcessActivityNotification> _current = new();

        private IDisposable? _subscription;

        public ActivityLimiter(ActivityMonitor activityMonitor, MonitorConfiguration configuration, ILogger<ActivityMonitor> logger)
        {
            _activityMonitor = activityMonitor;
            _logger = logger;
            _configuration = configuration;
        }
        public void Start()
        {
            _subscription = _activityMonitor.SpentTime.Subscribe(processesActivity =>
            {
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
                var processMatcher = _configuration.Processes[processActivity.ProcessName].Matcher;
                var processes = processMatcher == null
                    ? Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processActivity.ProcessName))
                    : Process.GetProcesses().Where(p => processMatcher(p.ProcessName)).ToArray();

                foreach (var process in processes)
                {
                    process.Kill();
                    _logger.LogInformation($"Stopped {_configuration.Processes[processActivity.ProcessName].AppName} (Process ID: {process.Id})");
                    MessageBox.Show($"Time limit {limit.Value:hh\\:mm\\:ss} exceeded for {processActivity.ProcessName}");
                }
                return;
            }

            var timeLeft = limit.Value - processActivity.SpentToday;
            if (!_current.TryGetValue(processActivity.ProcessName, out var previousActivity))
            {
                _current[processActivity.ProcessName] = processActivity;
                MessageBox.Show($"Left {timeLeft:hh\\:mm\\:ss} for {_configuration.Processes[processActivity.ProcessName].AppName}");
                return;
            }


            var previousActivityTimeLeft = limit.Value - previousActivity?.SpentToday ?? processActivity.SpentToday;
            foreach (var notificationsInterval in _configuration.NotificationIntervals.OrderBy(i => i))
            {
                if (timeLeft < notificationsInterval && (previousActivity == null || previousActivity == processActivity || previousActivityTimeLeft > notificationsInterval))
                {
                    MessageBox.Show($"Left {timeLeft:hh\\:mm\\:ss} for {_configuration.Processes[processActivity.ProcessName].AppName}");
                    break;
                }
            }

            _current[processActivity.ProcessName] = processActivity;
        }
        public void Stop() => _subscription?.Dispose();

        private TimeSpan? GetLimit(string processName)
        {
            var appLimits = _configuration.Processes.GetValueOrDefault(processName)?.Limits;
            if (appLimits == null)
                return null;

            if (appLimits.DateLimits.TryGetValue(DateTime.Today, out var dateLimit))
                return dateLimit;

            if (appLimits.DayLimits.TryGetValue(DateTime.Today.DayOfWeek, out var dayLimit))
                return dayLimit;

            return appLimits.Default;
        }
    }
}
