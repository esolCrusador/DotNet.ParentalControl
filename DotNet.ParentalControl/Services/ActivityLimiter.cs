using DotNet.ParentalControl.Configuration;
using DotNet.ParentalControl.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace DotNet.ParentalControl.Services
{

    public class ActivityLimiter
    {
        private readonly ActivityMonitor _activityMonitor;
        private readonly ILogger _logger;
        private readonly MonitorConfiguration _configuration;
        private readonly ConcurrentDictionary<string, Timings> _current = new();


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

            var processName = processActivity.ProcessName;
            var appLimits = _configuration.Processes.GetValueOrDefault(processName)?.Limits;
            if (appLimits == null)
                return;

            var dayLimit = GetDailyLimit(appLimits);
            var leftForBreak = GetTimeLeftForBreak(appLimits, processActivity.ProcessData);
            if (!dayLimit.HasValue && !leftForBreak.HasValue)
                return;

            var spentToday = processActivity.ProcessData.SpentToday;
            if (spentToday > dayLimit)
            {
                HandleStop(processName, $"Time limit {dayLimit.Value:hh\\:mm\\:ss} exceeded for {_configuration.Processes[processName].AppName}");
                return;
            }

            if (leftForBreak == TimeSpan.Zero)
            {
                HandleStop(processName, $"Max time without break {appLimits?.MaxTimeWithoutBreak:hh\\:mm\\:ss} exceeded for {_configuration.Processes[processName].AppName}. Take a break min: {appLimits?.MinBreak:hh\\:mm\\:ss}");
                return;
            }

            var timeLeftToday = dayLimit - spentToday;
            if (!_current.TryGetValue(processName, out var previousActivity))
                Task.Run(() => MessageBox.Show(GetTimeReminder(appLimits, timeLeftToday, leftForBreak, processName)));
            else
            {
                var previousActivityTimeLeft = previousActivity.LeftToday;
                var previousLeftForBreak = previousActivity.LeftBeforeBreak;
                foreach (var notificationsInterval in _configuration.NotificationIntervals.OrderBy(i => i))
                {
                    if (timeLeftToday < notificationsInterval && previousActivityTimeLeft > notificationsInterval
                        || leftForBreak < notificationsInterval && previousLeftForBreak > notificationsInterval)
                    {
                        Task.Run(() => MessageBox.Show(GetTimeReminder(appLimits, timeLeftToday, leftForBreak, processName)));
                        break;
                    }
                }
            }

            _current[processActivity.ProcessName] = new Timings { LeftToday = timeLeftToday, LeftBeforeBreak = leftForBreak };
        }
        private string GetTimeReminder(LimitsConfiguration appLimits, TimeSpan? timeLeftToday, TimeSpan? leftForBreak, string processName)
        {
            var message = new StringBuilder();
            if (timeLeftToday.HasValue)
                message.Append($"Today left {timeLeftToday:hh\\:mm\\:ss}. ");
            if (appLimits.MinBreak.HasValue)
                message.Append($"Min break is {appLimits.MinBreak:hh\\:mm\\:ss}. Next break starts in {leftForBreak:hh\\:mm\\:ss}. ");
            message.Append($"Application: {_configuration.Processes[processName].AppName}");

            return message.ToString();
        }
        private void HandleStop(string proccessName, string message)
        {
            var processMatcher = _configuration.Processes[proccessName].Matcher;
            var processes = processMatcher == null
                ? Process.GetProcessesByName(Path.GetFileNameWithoutExtension(proccessName))
                : Process.GetProcesses().Where(p => processMatcher(p.ProcessName)).ToArray();

            foreach (var process in processes)
            {
                process.Kill();
                _logger.LogInformation($"Stopped {_configuration.Processes[proccessName].AppName} (Process ID: {process.Id})");
            }
            Task.Run(() => MessageBox.Show(message));
        }
        public void Stop() => _subscription?.Dispose();

        private TimeSpan? GetTimeLeftForBreak(LimitsConfiguration appLimits, ProcessData processData)
        {
            if (appLimits.MaxTimeWithoutBreak == null || appLimits.MinBreak == null)
                return null;

            var checkPeriod = appLimits.MaxTimeWithoutBreak.Value + appLimits.MinBreak.Value;

            var spentForPeriod = processData.SpentForPeriod(processData.LastUpdated - checkPeriod);
            if (spentForPeriod > appLimits.MaxTimeWithoutBreak.Value)
                return TimeSpan.Zero;

            return appLimits.MaxTimeWithoutBreak.Value - spentForPeriod;
        }

        private TimeSpan? GetDailyLimit(LimitsConfiguration appLimits)
        {
            if (appLimits.DateLimits.TryGetValue(DateTime.Today, out var dateLimit))
                return dateLimit;

            if (appLimits.DayLimits.TryGetValue(DateTime.Today.DayOfWeek, out var dayLimit))
                return dayLimit;

            return appLimits.Default;
        }

        private class Timings
        {
            public required TimeSpan? LeftToday { get; set; }
            public required TimeSpan? LeftBeforeBreak { get; set; }
        }
    }
}
