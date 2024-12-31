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
        public TimeSpan ActivityCheckPeriod { get; set; } = TimeSpan.FromMinutes(1);
        public TimeSpan StateSavePeriod { get; set; } = TimeSpan.FromMinutes(1);
        public Dictionary<string, AppLimits> AppLimits { get; set; } = new Dictionary<string, AppLimits>();
        public TimeSpan[] NotificationIntervals = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1)];
    }

    public class AppLimits
    {
        public TimeSpan? Default { get; set; }
        public Dictionary<DayOfWeek, TimeSpan> DayLimit { get; set; } = new Dictionary<DayOfWeek, TimeSpan>();
        public Dictionary<DateTime, TimeSpan> DateLimits { get; set; } = new Dictionary<DateTime, TimeSpan>();
    }
}
