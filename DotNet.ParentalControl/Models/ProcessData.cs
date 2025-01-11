using DotNet.ParentalControl.Extensions;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Linq;

namespace DotNet.ParentalControl.Models
{
    public class ProcessData
    {
        private DateTime? _oldestSession;
        public ConcurrentDictionary<DateTime, DaySessions> Sessions { get; set; } = [];
        public DateTime? OldestSession
        {
            get
            {
                if (_oldestSession == null && Sessions.Count > 0)
                    _oldestSession = Sessions.Keys.Min();

                return _oldestSession;
            }

            set => _oldestSession = value;
        }
        public ConcurrentDictionary<int, DateTime> StartedProcesses { get; set; } = [];
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public TimeSpan SpentForPeriod(DateTime from, DateTime? to = default)
        {
            TimeSpan spent = TimeSpan.Zero;

            to = DateTimeExtensions.Min(LastUpdated, to);
            var longestRunning = GetLongestRunningProcess();
            if (longestRunning != null)
                spent = to.Value - DateTimeExtensions.Max(longestRunning.Value, from);

            var minDay = from.Date;
            spent += SpentForPeriod(minDay, from, to.Value);

            var maxDay = to.Value.Date;
            var diff = (maxDay - minDay).TotalDays;

            for (int i = 1; i <= diff; i++)
                spent += SpentForPeriod(minDay.AddDays(i), from, to.Value);

            return spent;
        }
        private TimeSpan SpentForPeriod(DateTime day, DateTime from, DateTime to)
        {
            var sessions = Sessions.GetValueOrDefault(day);
            if (sessions == null)
                return TimeSpan.Zero;

            return sessions.Sessions.Where(range => range.Start.IsBetween(from, to) || range.End.IsBetween(from, to))
                .Aggregate(TimeSpan.Zero, (agg, range) =>
                {
                    return agg + (DateTimeExtensions.Min(to, range.End) - DateTimeExtensions.Max(from, range.Start));
                });
        }
        public TimeSpan SpentToday
        {
            get
            {
                var spent = Sessions.GetValueOrDefault(DateTime.Today)?.TotalSpent ?? TimeSpan.Zero;
                var longestRunning = GetLongestRunningProcess();
                if (longestRunning != null)
                    spent += LastUpdated - DateTimeExtensions.Max(longestRunning, DateTime.Today);

                return spent;
            }
        }

        public ProcessData()
        {
        }

        public ProcessData(ProcessData processData)
        {
            Sessions = new ConcurrentDictionary<DateTime, DaySessions>(processData.Sessions.ToDictionary(
                kvp => kvp.Key,
                kvp => new DaySessions { Sessions = kvp.Value.Sessions.Select(dr => new DateRange(dr)).ToList() }
            ));
            StartedProcesses = new ConcurrentDictionary<int, DateTime>(processData.StartedProcesses);
            LastUpdated = processData.LastUpdated;
        }

        private DateTime? GetLongestRunningProcess() => StartedProcesses.Values
            .Select(p => (DateTime?)p)
            .DefaultIfEmpty(null).Min();
    }
}