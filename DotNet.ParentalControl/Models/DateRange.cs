namespace DotNet.ParentalControl.Models
{
    class DateRange
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public DateRange()
        {
        }
        public DateRange(DateRange dateRange)
        {
            Start = dateRange.Start;
            End = dateRange.End;
        }
        public TimeSpan Duration => End - Start;
    }
}