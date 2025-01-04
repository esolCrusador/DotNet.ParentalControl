namespace DotNet.ParentalControl.Models
{
    public class ProcessActivityNotification
    {
        public required string ProcessName { get; set; }
        public required bool IsActive { get; set; }
        public required ProcessData ProcessData { get; set; }
    }
}