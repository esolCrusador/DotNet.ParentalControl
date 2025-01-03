﻿using DotNet.ParentalControl.Models;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Management;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Encodings.Web;
using System.Text.Json;
using Wisk.ParentalControl;
using Wisk.ParentalControl.Models;
using static System.Windows.Forms.Design.AxImporter;

namespace ParentalControlPoc.Services
{
    public class ActivityMonitor : IDisposable
    {
        private ConcurrentDictionary<string, ProcessData>? _processes;
        private ConcurrentDictionary<string, ProcessData> Processes => _processes ?? throw new ArgumentNullException(nameof(_processes), "Not initialized");

        private readonly MonitorConfiguration _options;
        private readonly ILogger _logger;
        private readonly IObservable<long> _refreshTimer;
        private readonly IObservable<long> _saveStateTimer;

        private ManagementEventWatcher? _startWatcher;
        private ManagementEventWatcher? _stopWatcher;
        private IDisposable? _refreshes;
        private IDisposable? _saveState;


        private ISubject<List<ProcessActivityNotification>> _notifyAboutSpentTime;

        public IObservable<List<ProcessActivityNotification>> SpentTime;

        public ActivityMonitor(IOptions<MonitorConfiguration> options, ILogger<ActivityMonitor> logger)
        {
            _options = options.Value;
            _logger = logger;
            _notifyAboutSpentTime = new ReplaySubject<List<ProcessActivityNotification>>(1);
            SpentTime = _notifyAboutSpentTime.DistinctUntilChanged();

            _refreshTimer = Observable.Interval(_options.ActivityCheckPeriod);
            _saveStateTimer = Observable.Interval(_options.StateSavePeriod).StartWith(0);
        }

        public void Dispose()
        {
            _notifyAboutSpentTime.OnCompleted();
        }

        public void Start()
        {
            _options.Initialize();
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

            _logger.LogInformation("Monitoring Notepad++ process. Press Enter to exit.");
        }

        private string GetFilter() => $" WHERE {string.Join(" OR ", _options.AppLimits.Keys.Select(k =>
        {
            if (k.Contains("*"))
                return $"ProcessName like '{k.Replace("*", "%")}'";

            return $"ProcessName = '{k}'";
        }))}";

        public void Stop()
        {
            _startWatcher?.Stop();
            _stopWatcher?.Stop();
            _refreshes?.Dispose();
            _saveState?.Dispose();

            SaveState();


            // Calculate total time
            var processes = Processes.ToDictionary(p => p.Key, p => p.Value.SpentToday);
            _logger.LogInformation("Spent today: {Processes}", JsonSerializer.Serialize(processes,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                })
            );
        }

        private ConcurrentDictionary<string, ProcessData> LoadState()
        {
            var fileName = _options.StateFile;
            ConcurrentDictionary<string, ProcessData> loggedProcesses;
            if (!File.Exists(fileName))
                loggedProcesses = new();
            else
            {
                using var file = File.OpenRead(fileName);
                loggedProcesses = JsonSerializer.Deserialize<ConcurrentDictionary<string, ProcessData>>(file)
                    ?? new();
            }

            if (loggedProcesses.Keys.Any(processName => !_options.AppLimits.ContainsKey(processName)))
                foreach (var process in loggedProcesses.Keys.Where(processName => !_options.AppLimits.ContainsKey(processName)).ToList())
                    loggedProcesses.TryRemove(process, out _);

            Process[] processList = Process.GetProcesses();
            var runningProcesses = processList
                .Select(p => new KeyValuePair<string?, Process>(_options.FindProcessName($"{p.ProcessName}.exe"), p))
                .Where(kvp => kvp.Key != null)
                .GroupBy(p => p.Key!, kvp => kvp.Value)
                .ToDictionary(g => g.Key, p => p.ToDictionary(p => p.Id, p => p.StartTime));

            foreach (var process in _options.AppLimits.Keys)
            {
                var pd = loggedProcesses.GetOrAdd(process, _ => new ProcessData());
                var runningProcess = runningProcesses.GetValueOrDefault(process);

                if (!pd.StartedProcesses.IsEmpty)
                    foreach (var p in pd.StartedProcesses.Where(sp => runningProcess?.ContainsKey(sp.Key) != true))
                    {
                        if (!pd.Sessions.TryGetValue(pd.LastUpdated.Date, out var daySesions))
                            pd.Sessions.Add(pd.LastUpdated.Date, daySesions = new DaySessions());

                        daySesions.Sessions.Add(new DateRange { Start = p.Value, End = pd.LastUpdated });
                    }

                pd.StartedProcesses = new ConcurrentDictionary<int, DateTime>(runningProcess ?? []);
                pd.LastUpdated = DateTime.Now;
            }

            return loggedProcesses;
        }

        private void SaveState()
        {
            foreach (var process in Processes.Values)
                process.LastUpdated = DateTime.Now;

            if (!Directory.Exists(Path.GetDirectoryName(_options.StateFile)))
                Directory.CreateDirectory(Path.GetDirectoryName(_options.StateFile)!);

            using var file = File.Create(_options.StateFile);
            JsonSerializer.Serialize(file, Processes, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }

        private void ProcessStarted(object sender, EventArrivedEventArgs e)
        {
            var eventProcessName = (string)e.NewEvent.Properties["ProcessName"].Value;
            var processName = _options.FindProcessName(eventProcessName);
            if (processName == null)
            {
                _logger.LogError($"Could not find process {eventProcessName}");
                return;
            }

            var processes = Processes.GetOrAdd(processName, _ => new());

            int processId = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
            var created = DateTime.FromFileTime(Convert.ToInt64(e.NewEvent.Properties["Time_Created"].Value));

            processes.StartedProcesses.TryAdd(processId, created);
            processes.LastUpdated = DateTime.Now;

            _logger.LogInformation($"Notepad++ started at {processes.StartedProcesses.GetValueOrDefault(processId)} (Process ID: {processId})");

            _notifyAboutSpentTime.OnNext([new ProcessActivityNotification
            {
                ProcessName = processName,
                IsActive = true,
                SpentToday = processes.SpentToday
            }]);
        }

        private void ProcessStopped(object sender, EventArrivedEventArgs e)
        {
            var eventProcessName = (string)e.NewEvent.Properties["ProcessName"].Value;
            var processName = _options.FindProcessName(eventProcessName);
            if(processName == null)
            {
                _logger.LogError($"Could not find process {eventProcessName}");
                return;
            }

            int processId = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
            var processes = Processes.GetOrAdd(processName, _ => new());

            if (processes.StartedProcesses.TryGetValue(processId, out var startTime))
            {
                DateTime stopTime = DateTime.Now;
                TimeSpan runTime = stopTime - startTime;
                _logger.LogInformation($"Notepad++ stopped at {stopTime} (Process ID: {processId}). Run time: {runTime:hh\\:mm\\:ss}");
                processes.StartedProcesses.TryRemove(processId, out _);

                if (!processes.Sessions.TryGetValue(DateTime.Today, out var todaySessions))
                    processes.Sessions.Add(DateTime.Today, todaySessions = new());

                todaySessions.Sessions.Add(new DateRange { Start = startTime, End = stopTime });
                processes.LastUpdated = DateTime.Now;

                _notifyAboutSpentTime.OnNext([new ProcessActivityNotification
                {
                    ProcessName = processName,
                    IsActive = !processes.StartedProcesses.IsEmpty,
                    SpentToday = processes.SpentToday
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
                        SpentToday = processData.SpentToday
                    });
            }

            if (notifications.Count > 0)
                _notifyAboutSpentTime.OnNext(notifications);
        }
    }
}