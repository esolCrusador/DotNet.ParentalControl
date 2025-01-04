using System.Reactive.Linq;

namespace DotNet.ParentalControl.Models
{
    public class DaySessions
    {
        public List<DateRange> Sessions { get; set; } = [];
        public TimeSpan TotalSpent => Sessions.Aggregate(TimeSpan.Zero, (agg, s) => agg + s.Duration);
    }
}