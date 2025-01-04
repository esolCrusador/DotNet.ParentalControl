namespace DotNet.ParentalControl.Configuration
{
    public class LimitsConfiguration
    {
        public TimeSpan? Default { get; }
        public Dictionary<DayOfWeek, TimeSpan> DayLimits { get; }
        public Dictionary<DateTime, TimeSpan> DateLimits { get; }

        public LimitsConfiguration(LimitsOptions limitsOptions)
        {
            Default = limitsOptions.Default;
            DayLimits = limitsOptions.DayLimits;
            DateLimits = limitsOptions.DateLimits.ToDictionary(dl => dl.Date.Date, dl => dl.Limit);
        }
    }
}
