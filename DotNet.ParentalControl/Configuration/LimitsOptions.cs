namespace DotNet.ParentalControl.Configuration
{
    public class LimitsOptions
    {
        public TimeSpan? Default { get; set; }
        public Dictionary<DayOfWeek, TimeSpan> DayLimits { get; set; } = new Dictionary<DayOfWeek, TimeSpan>();
        public List<DateLimit> DateLimits { get; set; } = new List<DateLimit>();
        public TimeSpan? MaxPlayWithoutBreak { get; set; }
        public TimeSpan? MinBreak { get; set; }

        public class DateLimit
        {
            public DateTime Date { get; set; }
            public TimeSpan Limit { get; set; }
        }
    }
}
