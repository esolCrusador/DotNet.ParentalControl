namespace DotNet.ParentalControl.Configuration
{
    public class ProcessMonitor
    {
        public required string AppName { get; set; }
        public required Func<string, bool>? Matcher { get; set; }
        public required LimitsConfiguration Limits { get; set; }
    }
}
