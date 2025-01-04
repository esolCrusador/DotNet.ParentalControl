namespace DotNet.ParentalControl.Configuration
{
    public class MonitorOptions
    {
        public bool LogAllProcesses { get; set; }
        public string StateFile { get; set; } = "State.json";
        public TimeSpan ActivityCheckPeriod { get; set; } = TimeSpan.FromSeconds(20);
        public TimeSpan StateSavePeriod { get; set; } = TimeSpan.FromMinutes(1);
        public Dictionary<string, AppMonitor> Applications { get; set; } = new Dictionary<string, AppMonitor>();
        public TimeSpan[] NotificationIntervals = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1)];
    }
}
