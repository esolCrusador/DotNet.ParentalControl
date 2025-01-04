namespace DotNet.ParentalControl.Configuration
{
    public class AppMonitor
    {
        public Func<string, bool>? Matcher { get; set; }
        public string? AppName { get; set; }
        public List<string> Processes { get; set; } = new List<string>();

        public required LimitsOptions Limits { get; set; }
    }
}
