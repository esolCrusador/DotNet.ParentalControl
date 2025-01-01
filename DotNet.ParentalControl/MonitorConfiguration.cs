using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Wisk.ParentalControl
{
    public class MonitorConfiguration
    {
        private static readonly Regex SpecialPath = new Regex("\\%(?<name>[^\\%]+)\\%\\\\", RegexOptions.Compiled);
        private string _stateFile = "State.json";
        private ConcurrentDictionary<string, string> _stateFileFullPath = [];

        public string StateFile
        {
            get
            {
                return _stateFileFullPath.GetOrAdd(_stateFile, path =>
                {
                    var specialPath = SpecialPath.Match(path);
                    if (specialPath.Success)
                    {
                        var specialFolder = specialPath.Groups["name"].Value switch
                        {
                            "LOCALAPPDATA" => Environment.SpecialFolder.LocalApplicationData,
                            _ => throw new NotSupportedException($"Not supported %{specialPath}%")
                        };

                        return Environment.GetFolderPath(specialFolder) + "\\" + path.Substring(specialPath.Length);
                    }
                    else
                        return path;
                });
            }
            set
            {
                _stateFile = value;
                _stateFileFullPath.Clear();
            }
        }
        public TimeSpan ActivityCheckPeriod { get; set; } = TimeSpan.FromSeconds(20);
        public TimeSpan StateSavePeriod { get; set; } = TimeSpan.FromMinutes(1);
        public Dictionary<string, AppLimits> AppLimits { get; set; } = new Dictionary<string, AppLimits>(StringComparer.OrdinalIgnoreCase);
        private Func<string, string?>? _matcher;
        public TimeSpan[] NotificationIntervals = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1)];

        public void Initialize()
        {
            foreach (var (processName, appLimits) in AppLimits)
                if (processName.Contains('*'))
                {
                    var matcher = new Regex(Regex.Escape(processName).Replace("\\*", ".*"), RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    appLimits.Matcher = matcher.IsMatch;
                }

            var matchers = AppLimits.Where(kvp => kvp.Value.Matcher != null)
                .Select(kvp => new KeyValuePair<Func<string, bool>, string>(kvp.Value.Matcher!, kvp.Key))
                .ToArray();
            _matcher = name =>
            {
                for (int i = 0; i < matchers.Length; i++)
                {
                    if (matchers[i].Key(name))
                        return matchers[i].Value;
                }

                return null;
            };
        }

        public string? FindProcessName(string processName)
        {
            if (AppLimits.ContainsKey(processName))
                return processName;

            var matcher = _matcher ?? throw new InvalidOperationException($"{nameof(MonitorConfiguration)} is not initialized");
            return matcher(processName);
        }
    }

    public class AppLimits
    {
        public Func<string, bool>? Matcher { get; set; }
        public TimeSpan? Default { get; set; }
        public Dictionary<DayOfWeek, TimeSpan> DayLimit { get; set; } = new Dictionary<DayOfWeek, TimeSpan>();
        public Dictionary<DateTime, TimeSpan> DateLimits { get; set; } = new Dictionary<DateTime, TimeSpan>();
    }
}
