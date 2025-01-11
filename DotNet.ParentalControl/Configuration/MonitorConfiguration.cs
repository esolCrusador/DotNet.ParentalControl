using DotNet.ParentalControl.Extensions;
using Microsoft.Extensions.Options;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;

namespace DotNet.ParentalControl.Configuration
{
    public class MonitorConfiguration : IDisposable
    {
        private readonly IDisposable? _changeTracker;
        private readonly Subject<Unit> _changed;

        public TimeSpan ActivityCheckPeriod { get; private set; }
        public TimeSpan StateSavePeriod { get; private set; }
        public bool LogAllProcesses { get; private set; }
        public string StateFile { get; private set; }
        public TimeSpan[] NotificationIntervals { get; private set; }
        public TimeSpan KeepSessionsHistory { get; set; }

        public Dictionary<string, ProcessMonitor> Processes { get; private set; }
        public Func<string, string?> GetProcessName { get; private set; }
        public IObservable<Unit> Changed { get; }

        public MonitorConfiguration(IOptions<MonitorOptions> options, IOptionsMonitor<MonitorOptions> optionsMonitor)
        {
            InitializeConfiguration(options.Value);
            _changeTracker = optionsMonitor.OnChange(changedOptions =>
            {
                InitializeConfiguration(changedOptions);
                _changed?.OnNext(Unit.Default);
            });
            _changed = new Subject<Unit>();
            Changed = _changed.AsObservable();
        }

        [MemberNotNull(nameof(StateFile), nameof(NotificationIntervals), nameof(Processes), nameof(GetProcessName))]
        private void InitializeConfiguration(MonitorOptions options)
        {
            ActivityCheckPeriod = options.ActivityCheckPeriod;
            StateSavePeriod = options.StateSavePeriod;
            LogAllProcesses = options.LogAllProcesses;
            StateFile = DirectoryExtensions.ResolveSpecialFolders(options.StateFile);
            NotificationIntervals = options.NotificationIntervals;
            KeepSessionsHistory = options.KeepSessionsHistory;

            Processes = GetProcessLimits(options);
            GetProcessName = CreateGetProcessName(Processes);
        }

        public void Dispose()
        {
            _changeTracker?.Dispose();
            _changed.OnCompleted();
        }

        public string? FindProcessName(string processName)
        {
            if (Processes.ContainsKey(processName))
                return processName;

            return GetProcessName(processName);
        }

        private static Dictionary<string, ProcessMonitor> GetProcessLimits(MonitorOptions options)
        {
            return options.Applications.SelectMany(kvp =>
            {
                return kvp.Value.Processes.Select(processName =>
                {
                    Func<string, bool>? matcher = null;
                    if (processName.Contains('*'))
                    {
                        var regex = new Regex(Regex.Escape(processName).Replace("\\*", ".*"), RegexOptions.Compiled | RegexOptions.IgnoreCase);
                        matcher = regex.IsMatch;
                    }

                    return new KeyValuePair<string, ProcessMonitor>(
                                        processName,
                                        new ProcessMonitor
                                        {
                                            AppName = kvp.Key,
                                            Matcher = matcher,
                                            Limits = new LimitsConfiguration(kvp.Value.Limits)
                                        });
                });
            }).ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        }

        private static Func<string, string?> CreateGetProcessName(Dictionary<string, ProcessMonitor> processLimits)
        {
            var matchers = processLimits.Where(kvp => kvp.Value.Matcher != null)
                .Select(kvp => new KeyValuePair<Func<string, bool>, string>(kvp.Value.Matcher!, kvp.Key))
                .ToArray();

            return name =>
            {
                for (int i = 0; i < matchers.Length; i++)
                {
                    if (matchers[i].Key(name))
                        return matchers[i].Value;
                }

                return null;
            };
        }
    }
}
