﻿using System.Reactive.Linq;

namespace Wisk.ParentalControl.Models
{
    class DaySessions
    {
        public List<DateRange> Sessions { get; set; } = [];
        public TimeSpan TotalSpent => Sessions.Aggregate(TimeSpan.Zero, (agg, s) => agg + s.Duration);
    }
}