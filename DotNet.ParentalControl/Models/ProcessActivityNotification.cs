namespace DotNet.ParentalControl.Models
{
    public class ProcessActivityNotification : IEquatable<ProcessActivityNotification>
    {
        public required string ProcessName { get; set; }
        public required bool IsActive { get; set; }
        public required TimeSpan SpentToday { get; set; }

        public override bool Equals(object? obj)
        {
            return base.Equals(obj) || obj is ProcessActivityNotification processActivityNotification && Equals(processActivityNotification);
        }

        public override int GetHashCode()
        {
            return ProcessName.GetHashCode() ^ IsActive.GetHashCode() ^ SpentToday.GetHashCode();
        }

        public bool Equals(ProcessActivityNotification? other)
        {
            return other != null && ProcessName == other.ProcessName && IsActive == other.IsActive && SpentToday == other.SpentToday;
        }
    }
}