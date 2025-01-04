using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using static System.Windows.Forms.Design.AxImporter;

namespace DotNet.ParentalControl.Configuration
{
    public class MonitorConfiguration
    {
        private static readonly Regex SpecialPath = new Regex("\\%(?<name>[^\\%]+)\\%\\\\", RegexOptions.Compiled);

        public TimeSpan ActivityCheckPeriod { get; }
        public TimeSpan StateSavePeriod { get; }
        public bool LogAllProcesses { get; }
        public string StateFile { get; }
        public TimeSpan[] NotificationIntervals { get; }

        public Dictionary<string, ProcessMonitor> Processes { get; }
        public Func<string, string?> GetProcessName { get; }

        public MonitorConfiguration(IOptions<MonitorOptions> options)
        {
            ActivityCheckPeriod = options.Value.ActivityCheckPeriod;
            StateSavePeriod = options.Value.StateSavePeriod;
            LogAllProcesses = options.Value.LogAllProcesses;
            StateFile = GetStateFile(options.Value);
            NotificationIntervals = options.Value.NotificationIntervals;

            Processes = GetProcessLimits(options.Value);
            GetProcessName = CreateGetProcessName(Processes);
        }

        public string? FindProcessName(string processName)
        {
            if (Processes.ContainsKey(processName))
                return processName;

            return GetProcessName(processName);
        }

        private static string GetStateFile(MonitorOptions options)
        {
            var path = options.StateFile;
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
