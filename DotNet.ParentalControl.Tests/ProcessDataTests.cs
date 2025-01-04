using DotNet.ParentalControl.Models;
using FluentAssertions;
using System.Collections.Concurrent;

namespace DotNet.ParentalControl.Tests
{
    [Trait("Category", "Fast")]
    public class ProcessDataTests
    {
        [Fact]
        public void SpentToday_Should_Calculate_Time_For_Single_Process()
        {
            var now = DateTime.Today.Add(TimeSpan.FromHours(10));
            var processData = new ProcessData
            {
                StartedProcesses = new ConcurrentDictionary<int, DateTime>
                {
                    [1] = now - TimeSpan.FromHours(3)
                },
                LastUpdated = now
            };

            processData.SpentToday.Should().Be(TimeSpan.FromHours(3));
        }

        [Fact]
        public void SpentToday_Should_Not_Take_InToACount_Yesterday_Activity()
        {
            var now = DateTime.Today.Add(TimeSpan.FromHours(1));
            var processData = new ProcessData
            {
                StartedProcesses = new ConcurrentDictionary<int, DateTime>
                {
                    [1] = now - TimeSpan.FromHours(3)
                },
                LastUpdated = now
            };

            processData.SpentToday.Should().Be(TimeSpan.FromHours(1));
        }

        [Fact]
        public void SpentToday_Should_Take_Into_A_Count_Sessions()
        {
            var now = DateTime.Today.Add(TimeSpan.FromHours(10));
            var processData = new ProcessData
            {
                Sessions = new Dictionary<DateTime, DaySessions>
                {
                    [DateTime.Today] = new DaySessions
                    {
                        Sessions =
                        {
                            new DateRange{Start = now.AddHours(-3), End = now.AddHours(-1)}
                        }
                    }
                },
                StartedProcesses = new ConcurrentDictionary<int, DateTime>
                {
                    [1] = now - TimeSpan.FromHours(3)
                },
                LastUpdated = now
            };

            processData.SpentToday.Should().Be(TimeSpan.FromHours(5));
        }

        [Fact]
        public void SpentToday_Should_Count_By_Sessions_Only_If_No_Proccesses()
        {
            var now = DateTime.Today.Add(TimeSpan.FromHours(10));
            var processData = new ProcessData
            {
                Sessions = new Dictionary<DateTime, DaySessions>
                {
                    [DateTime.Today] = new DaySessions
                    {
                        Sessions =
                        {
                            new DateRange{Start = now.AddHours(-3), End = now.AddHours(-1)}
                        }
                    }
                },
                LastUpdated = now
            };

            processData.SpentToday.Should().Be(TimeSpan.FromHours(2));
        }

        [Fact]
        public void SpentForPeriod_Should_Count_By_Recently_Running_Proccesses()
        {
            var now = DateTime.Today.Add(TimeSpan.FromHours(10));
            var processData = new ProcessData
            {
                StartedProcesses = new ConcurrentDictionary<int, DateTime>
                {
                    [1] = now - TimeSpan.FromHours(1)
                },
                LastUpdated = now
            };

            processData.SpentForPeriod(now.AddHours(-3)).Should().Be(TimeSpan.FromHours(1));
        }

        [Fact]
        public void SpentForPeriod_Should_Count_By_Long_Running_Proccesses()
        {
            var now = DateTime.Today.Add(TimeSpan.FromHours(10));
            var processData = new ProcessData
            {
                StartedProcesses = new ConcurrentDictionary<int, DateTime>
                {
                    [1] = now - TimeSpan.FromHours(3)
                },
                LastUpdated = now
            };

            processData.SpentForPeriod(now.AddHours(-1)).Should().Be(TimeSpan.FromHours(1));
        }

        [Fact]
        public void SpentForPeriod_Should_Cut_Length_Of_Process()
        {
            var now = DateTime.Today.Add(TimeSpan.FromHours(10));
            var processData = new ProcessData
            {
                StartedProcesses = new ConcurrentDictionary<int, DateTime>
                {
                    [1] = now - TimeSpan.FromHours(3)
                },
                LastUpdated = now
            };

            processData.SpentForPeriod(now.AddHours(-1), now.AddMinutes(-30)).Should().Be(TimeSpan.FromHours(0.5));
        }

        [Fact]
        public void SpentForPeriod_Should_Count_By_Long_Running_Proccesses_And_Sessions()
        {
            var now = DateTime.Today.Add(TimeSpan.FromHours(10));
            var processData = new ProcessData
            {
                StartedProcesses = new ConcurrentDictionary<int, DateTime>
                {
                    [1] = now - TimeSpan.FromMinutes(30)
                },
                Sessions = new Dictionary<DateTime, DaySessions>
                {
                    [DateTime.Today] = new DaySessions { Sessions = { new DateRange { Start = now.AddHours(-4), End = now.AddHours(-1) } } }
                },
                LastUpdated = now
            };

            processData.SpentForPeriod(now.AddHours(-2)).Should().Be(TimeSpan.FromHours(1.5));
        }

        [Fact]
        public void SpentForPeriod_Should_Take_Into_A_Count_Previous_Days()
        {
            var now = DateTime.Today.Add(TimeSpan.FromHours(1));
            var processData = new ProcessData
            {
                StartedProcesses = new ConcurrentDictionary<int, DateTime>
                {
                    [1] = now - TimeSpan.FromMinutes(30)
                },
                Sessions = new Dictionary<DateTime, DaySessions>
                {
                    [DateTime.Today.AddDays(-1)] = new DaySessions { Sessions = { new DateRange { Start = now.AddHours(-4), End = now.AddHours(-1) } } },
                    [DateTime.Today] = new DaySessions { Sessions = {new DateRange { Start = now.Date, End = now.Date.AddMinutes(10)} }}
                },
                LastUpdated = now
            };

            processData.SpentForPeriod(now.AddHours(-2)).Should().Be(TimeSpan.FromMinutes(100));
        }

        [Fact]
        public void SpentForPeriod_Should_Not_Take_Into_A_Count_Sessions_Not_From_Period()
        {
            var now = DateTime.Today.Add(TimeSpan.FromHours(1));
            var processData = new ProcessData
            {
                StartedProcesses = new ConcurrentDictionary<int, DateTime>
                {
                    [1] = now - TimeSpan.FromMinutes(30)
                },
                Sessions = new Dictionary<DateTime, DaySessions>
                {
                    [DateTime.Today.AddDays(-1)] = new DaySessions { Sessions = { new DateRange { Start = now.AddHours(-4), End = now.AddHours(-3) } } }
                },
                LastUpdated = now
            };

            processData.SpentForPeriod(now.AddHours(-2)).Should().Be(TimeSpan.FromHours(0.5));
        }
    }
}