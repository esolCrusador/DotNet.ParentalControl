using DotNet.ParentalControl.Configuration;
using DotNet.ParentalControl.Models;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Management;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DotNet.ParentalControl.Services
{
    public class ActivityMonitor : IDisposable
    {
        private ConcurrentDictionary<string, ProcessData>? _processes;
        private ConcurrentDictionary<string, ProcessData> Processes => _processes ?? throw new ArgumentNullException(nameof(_processes), "Not initialized");

        private readonly MonitorConfiguration _configuration;
        private readonly object _saveStateLock = new object();
        private readonly JsonSerializerOptions _writeSerializerOptions;
        private readonly ILogger _logger;
        private readonly IObservable<long> _refreshTimer;
        private readonly IObservable<long> _saveStateTimer;

        private ManagementEventWatcher? _startWatcher;
        private ManagementEventWatcher? _stopWatcher;
        private IDisposable? _refreshes;
        private IDisposable? _saveState;


        private ISubject<List<ProcessActivityNotification>> _notifyAboutSpentTime;

        public IObservable<List<ProcessActivityNotification>> SpentTime;

        public ActivityMonitor(MonitorConfiguration monitorConfiguration, JsonSerializerOptions writeSerializerOptions, ILogger<ActivityMonitor> logger)
        {
            _configuration = monitorConfiguration;
            _writeSerializerOptions = writeSerializerOptions;
            _logger = logger;
            _notifyAboutSpentTime = new ReplaySubject<List<ProcessActivityNotification>>(1);
            SpentTime = _notifyAboutSpentTime.DistinctUntilChanged();

            _refreshTimer = Observable.Interval(_configuration.ActivityCheckPeriod);
            _saveStateTimer = Observable.Interval(_configuration.StateSavePeriod).StartWith(0);
        }

        public void Dispose()
        {
            _notifyAboutSpentTime.OnCompleted();
        }

        public void Start()
        {
            _processes = LoadState();

            // Monitor process start events
            _startWatcher = new ManagementEventWatcher(
            new WqlEventQuery($"SELECT * FROM Win32_ProcessStartTrace {GetFilter()}"));
            _startWatcher.EventArrived += new EventArrivedEventHandler(ProcessStarted);
            _startWatcher.Start();

            // Monitor process stop events
            _stopWatcher = new ManagementEventWatcher(
            new WqlEventQuery($"SELECT * FROM Win32_ProcessStopTrace {GetFilter()}"));
            _stopWatcher.EventArrived += new EventArrivedEventHandler(ProcessStopped);
            _stopWatcher.Start();

            _refreshes = _refreshTimer.Subscribe(SendCurrentState);
            SendCurrentState(0);
            _saveState = _saveStateTimer.Subscribe(_ => SaveState());

            _logger.LogInformation($"Monitoring {string.Join(", ", _configuration.Processes.Values.Select(pl => pl.AppName).Distinct())} processes.");
        }

        private string GetFilter()
        {
            if (_configuration.LogAllProcesses)
                return "";

            return $" WHERE {string.Join(" OR ", _configuration.Processes.Keys.Select(k =>
            {
                if (k.Contains("*"))
                    return $"ProcessName like '{k.Replace("*", "%")}'";

                return $"ProcessName = '{k}'";
            }))}";
        }

        public void Stop()
        {
            _startWatcher?.Stop();
            _stopWatcher?.Stop();
            _refreshes?.Dispose();
            _saveState?.Dispose();

            SaveState();

            // Calculate total time
            var processes = Processes.ToDictionary(p => p.Key, p => p.Value.SpentToday);
            _logger.LogInformation("Spent today: {Processes}", JsonSerializer.Serialize(processes, _writeSerializerOptions));
        }

        private ConcurrentDictionary<string, ProcessData> LoadState()
        {
            var fileName = _configuration.StateFile;
            ConcurrentDictionary<string, ProcessData> loggedProcesses;
            if (!File.Exists(fileName))
                loggedProcesses = new();
            else
            {
                using var file = File.OpenRead(fileName);
                loggedProcesses = JsonSerializer.Deserialize<ConcurrentDictionary<string, ProcessData>>(file)
                    ?? new();
            }

            if (loggedProcesses.Keys.Any(processName => !_configuration.Processes.ContainsKey(processName)))
                foreach (var process in loggedProcesses.Keys.Where(processName => !_configuration.Processes.ContainsKey(processName)).ToList())
                    loggedProcesses.TryRemove(process, out _);

            Process[] processList = Process.GetProcesses();
            if (_configuration.LogAllProcesses)
                _logger.LogInformation("Running processes: {Processes}",
                    JsonSerializer.Serialize(processList
                    .Where(p =>
                    {
                        try
                        {
                            return p.ProcessName != "Idle" && p.StartTime > DateTime.MinValue;
                        }
                        catch
                        {
                            return false;
                        }
                    })
                    .Select(p => new { p.ProcessName, p.Id, p.StartTime })
                    , _writeSerializerOptions)
                );

            var runningProcesses = processList
                .Select(p => new KeyValuePair<string?, Process>(_configuration.FindProcessName($"{p.ProcessName}.exe"), p))
                .Where(kvp => kvp.Key != null)
                .GroupBy(p => p.Key!, kvp => kvp.Value)
                .ToDictionary(g => g.Key, p => p.ToDictionary(p => p.Id, p => p.StartTime));

            foreach (var process in _configuration.Processes.Keys)
            {
                var pd = loggedProcesses.GetOrAdd(process, _ => new ProcessData());
                var runningProcess = runningProcesses.GetValueOrDefault(process);

                if (!pd.StartedProcesses.IsEmpty)
                    foreach (var p in pd.StartedProcesses.Where(sp => runningProcess?.ContainsKey(sp.Key) != true))
                    {
                        if (!pd.Sessions.TryGetValue(pd.LastUpdated.Date, out var daySesions))
                            pd.Sessions.TryAdd(pd.LastUpdated.Date, daySesions = new DaySessions());

                        daySesions.Sessions.Add(new DateRange { Start = p.Value, End = pd.LastUpdated });
                    }

                pd.StartedProcesses = new ConcurrentDictionary<int, DateTime>(runningProcess ?? []);
                pd.LastUpdated = DateTime.Now;
            }

            return loggedProcesses;
        }

        private void SaveState()
        {
            if (Monitor.TryEnter(_saveStateLock))
            {
                try
                {
                    foreach (var process in Processes.Values)
                    {
                        if ((DateTime.Now - process.OldestSession) > _configuration.KeepSessionsHistory)
                        {
                            var oldDays = process.Sessions.Keys.Where(k => (DateTime.Now - k) > _configuration.KeepSessionsHistory).ToList();
                            foreach (var day in oldDays)
                                process.Sessions.TryRemove(day, out _);

                            process.OldestSession = null;
                        }

                        process.LastUpdated = DateTime.Now;
                    }

                    if (!Directory.Exists(Path.GetDirectoryName(_configuration.StateFile)))
                        Directory.CreateDirectory(Path.GetDirectoryName(_configuration.StateFile)!);

                    using var file = File.Create(_configuration.StateFile);
                    JsonSerializer.Serialize(file, Processes, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });
                }
                finally
                {
                    Monitor.Exit(_saveStateLock);
                }
            }
        }

        private void ProcessStarted(object sender, EventArrivedEventArgs e)
        {
            var eventProcessName = (string)e.NewEvent.Properties["ProcessName"].Value;
            if (_configuration.LogAllProcesses)
                _logger.LogInformation(
                    "Process Started: {Process}",
                    JsonSerializer.Serialize(e.NewEvent.Properties.Cast<PropertyData>().ToDictionary(pd => pd.Name, pd => pd.Value),
                    _writeSerializerOptions
                ));

            var processName = _configuration.FindProcessName(eventProcessName);
            if (processName == null)
            {
                _logger.LogError($"Could not find process {eventProcessName}");
                return;
            }

            var process = Processes.GetOrAdd(processName, _ => new());

            int processId = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
            var created = DateTime.FromFileTime(Convert.ToInt64(e.NewEvent.Properties["Time_Created"].Value));

            process.StartedProcesses.TryAdd(processId, created);
            process.LastUpdated = DateTime.Now;

            _logger.LogInformation($"Notepad++ started at {process.StartedProcesses.GetValueOrDefault(processId)} (Process ID: {processId})");

            _notifyAboutSpentTime.OnNext([new ProcessActivityNotification
            {
                ProcessName = processName,
                IsActive = true,
                ProcessData = process
            }]);
        }

        private void ProcessStopped(object sender, EventArrivedEventArgs e)
        {
            var eventProcessName = (string)e.NewEvent.Properties["ProcessName"].Value;
            if (_configuration.LogAllProcesses)
                _logger.LogInformation(
                    "Process Stopped: {Process}",
                    JsonSerializer.Serialize(e.NewEvent.Properties.Cast<PropertyData>().ToDictionary(pd => pd.Name, pd => pd.Value),
                    _writeSerializerOptions
                ));

            var processName = _configuration.FindProcessName(eventProcessName);
            if (processName == null)
            {
                _logger.LogError($"Could not find process {eventProcessName}");
                return;
            }

            int processId = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
            var process = Processes.GetOrAdd(processName, _ => new());

            if (process.StartedProcesses.TryGetValue(processId, out var startTime))
            {
                DateTime stopTime = DateTime.Now;
                TimeSpan runTime = stopTime - startTime;
                _logger.LogInformation($"Notepad++ stopped at {stopTime} (Process ID: {processId}). Run time: {runTime:hh\\:mm\\:ss}");
                process.StartedProcesses.TryRemove(processId, out _);

                if (!process.Sessions.TryGetValue(DateTime.Today, out var todaySessions))
                    process.Sessions.TryAdd(DateTime.Today, todaySessions = new());

                todaySessions.Sessions.Add(new DateRange { Start = startTime, End = stopTime });
                process.LastUpdated = DateTime.Now;

                _notifyAboutSpentTime.OnNext([new ProcessActivityNotification
                {
                    ProcessName = processName,
                    IsActive = !process.StartedProcesses.IsEmpty,
                    ProcessData = process
                }]);
            }
        }

        private void SendCurrentState(long interval)
        {
            var notifications = new List<ProcessActivityNotification>();
            foreach (var (processName, processData) in Processes)
            {
                processData.LastUpdated = DateTime.Now;
                if (!processData.StartedProcesses.IsEmpty)
                    notifications.Add(new ProcessActivityNotification
                    {
                        ProcessName = processName,
                        IsActive = true,
                        ProcessData = processData
                    });
            }

            if (notifications.Count > 0)
                _notifyAboutSpentTime.OnNext(notifications);
        }
    }
}