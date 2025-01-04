namespace DotNet.ParentalControl.Configuration
{
    public class LimitsConfiguration
    {
        public TimeSpan? Default { get; }
        public Dictionary<DayOfWeek, TimeSpan> DayLimits { get; }
        public Dictionary<DateTime, TimeSpan> DateLimits { get; }
        public TimeSpan? MaxTimeWithoutBreak { get; }
        public TimeSpan? MinBreak { get; }

        public LimitsConfiguration(LimitsOptions limitsOptions)
        {
            Default = limitsOptions.Default;
            DayLimits = limitsOptions.DayLimits;
            DateLimits = limitsOptions.DateLimits.ToDictionary(dl => dl.Date.Date, dl => dl.Limit);
            MaxTimeWithoutBreak = limitsOptions.MaxPlayWithoutBreak;
            MinBreak = limitsOptions.MinBreak;

            if (MaxTimeWithoutBreak == null && MinBreak != null)
                MaxTimeWithoutBreak = MinBreak;

            if (MaxTimeWithoutBreak != null && MinBreak == null)
                MinBreak = MaxTimeWithoutBreak;
        }
    }
}
