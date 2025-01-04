using System.Collections.Concurrent;
using System.Reactive.Linq;

namespace DotNet.ParentalControl.Models
{
    class ProcessData
    {
        public Dictionary<DateTime, DaySessions> Sessions { get; set; } = [];
        public ConcurrentDictionary<int, DateTime> StartedProcesses { get; set; } = [];
        public DateTime LastUpdated { get; set; } = DateTime.Now;
        public TimeSpan SpentToday
        {
            get
            {
                var sessions = Sessions.GetValueOrDefault(DateTime.Today)?.TotalSpent ?? TimeSpan.Zero;
                var longestProcess = StartedProcesses.Values.Select(p => LastUpdated - p).DefaultIfEmpty(TimeSpan.Zero).Max();

                return sessions + longestProcess;
            }
        }

        public ProcessData()
        {
        }

        public ProcessData(ProcessData processData)
        {
            Sessions = processData.Sessions.ToDictionary(
                kvp => kvp.Key,
                kvp => new DaySessions { Sessions = kvp.Value.Sessions.Select(dr => new DateRange(dr)).ToList() }
                );
            StartedProcesses = new ConcurrentDictionary<int, DateTime>(processData.StartedProcesses);
            LastUpdated = processData.LastUpdated;
        }
    }
}