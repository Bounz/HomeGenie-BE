using System;
using System.Collections.Generic;
using System.Linq;
using HomeGenie.Data;
using HomeGenie.Service.Logging;

namespace HomeGenie.Database
{
    public class StatisticsRepository : GenericRepository<StatisticsDbEntry>, IStatisticsRepository
    {
        public StatisticsRepository()
            : base(FilePaths.StatisticsDbFilePath)
        {
            Execute(collection =>
            {
                collection.EnsureIndex(x => x.TimeStart);
                collection.EnsureIndex(x => x.TimeEnd);
            });
        }

        public (DateTime start, DateTime end) GetDateRange()
        {
            return Execute(statistics =>
            {
                var start = DateTime.UtcNow;
                var end = DateTime.UtcNow;
                if (statistics.Count() == 0)
                    return (start, end);
                start = statistics.Min(x => x.TimeStart);
                end = statistics.Max(x => x.TimeEnd);
                return (start, end);
            });
        }

        /// <summary>
        /// Gets the parameters list.
        /// </summary>
        /// <returns>The parameters list.</returns>
        /// <param name="domain">Domain</param>
        /// <param name="address">Address</param>
        public List<string> GetParametersList(string domain, string address)
        {
            var deviceSpecified = !string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(address);
            return Execute(collection =>
            {
                var parameterList = (deviceSpecified
                        ? collection.Find(x => x.Domain == domain && x.Address == address)
                        : collection.FindAll()
                    ).Select(x => x.Parameter).Distinct().ToList();
                return parameterList;
            });
        }

        /// <summary>
        /// Gets the total counter.
        /// </summary>
        /// <returns>The total counter.</returns>
        /// <param name="parameterName">Parameter name.</param>
        /// <param name="timeScaleSeconds">Time scale seconds.</param>
        public double GetTotalCounter(string parameterName, double timeScaleSeconds)
        {
            return Execute(statistics =>
            {
                var statItems = statistics.Find(x => x.Parameter == parameterName);
                var total = statItems.Sum(x => x.AvgValue * (x.TimeEnd - x.TimeStart).TotalSeconds / timeScaleSeconds);
                return total;
            });
        }

        /// <summary>
        /// Gets the hourly counter.
        /// </summary>
        /// <returns>The hourly counter.</returns>
        /// <param name="domain">Domain.</param>
        /// <param name="address">Address.</param>
        /// <param name="parameterName">Parameter name.</param>
        /// <param name="timescaleseconds">Timescaleseconds.</param>
        /// <param name="startDate">Start date.</param>
        /// <param name="endDate">End date.</param>
        public List<StatisticsDbEntry> GetHourlyCounter(string domain,
            string address,
            string parameterName,
            double timescaleseconds,
            DateTime startDate, DateTime endDate)
        {
            return Execute(statistics =>
            {
                var values = new List<StatisticsDbEntry>();
                var statItems = string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(address)
                    ? statistics.Find(x => x.Parameter == parameterName &&
                                           x.TimeStart >= startDate &&
                                           x.TimeEnd <= endDate)
                    : statistics.Find(x => x.Parameter == parameterName &&
                                           x.TimeStart >= startDate &&
                                           x.TimeEnd <= endDate &&
                                           x.Address == address &&
                                           x.Domain == domain);

                var grouppedStats = statItems.GroupBy(x => new {x.Domain, x.Address, x.TimeStart.Hour});
                foreach (var grouppedStat in grouppedStats)
                {
                    values.Add(new StatisticsDbEntry
                    {
                        Domain = grouppedStat.Key.Domain,
                        Address = grouppedStat.Key.Address,
                        TimeStart = startDate.Date.AddHours(grouppedStat.Key.Hour),
                        TimeEnd = grouppedStat.First().TimeEnd,
                        AvgValue = grouppedStat.Sum(x => x.AvgValue * (x.TimeEnd - x.TimeStart).TotalSeconds / timescaleseconds)
                    });
                }

                return values;
            });
        }

        public List<IGrouping<StatGroup, StatisticsDbEntry>> GetGrouppedStats(
            string domain,
            string address,
            string parameterName,
            DateTime startDate, DateTime endDate
        )
        {
            return Execute(statistics =>
            {
                var statItems = string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(address)
                    ? statistics.Find(x => x.Parameter == parameterName &&
                                           x.TimeStart >= startDate &&
                                           x.TimeEnd <= endDate)
                    : statistics.Find(x => x.Parameter == parameterName &&
                                           x.TimeStart >= startDate &&
                                           x.TimeEnd <= endDate &&
                                           x.Address == address &&
                                           x.Domain == domain);

                var grouppedStats = statItems.GroupBy(x => new StatGroup
                {
                    Domain = x.Domain,
                    Address = x.Address,
                    Hour = x.TimeStart.Hour
                });
                return grouppedStats.ToList();
            });
        }

        public List<StatisticsDbEntry> GetStatsByParameter(
            string parameterName,
            DateTime startDate, DateTime endDate
        )
        {
            return Execute(statistics =>
            {
                var statItems = statistics.Find(x => x.Parameter == parameterName &&
                                                     x.TimeStart >= startDate &&
                                                     x.TimeEnd <= endDate);
                return statItems.OrderBy(x => x.TimeStart).ToList();
            });
        }

        public List<StatisticsDbEntry> GetStatsByParameterAndDevice(
            string domain,
            string address,
            string parameterName,
            DateTime startDate, DateTime endDate
        )
        {
            return Execute(statistics =>
            {
                var statItems = statistics.Find(x => x.Parameter == parameterName &&
                                                     x.TimeStart >= startDate &&
                                                     x.TimeEnd <= endDate &&
                                                     x.Address == address &&
                                                     x.Domain == domain);
                return statItems.OrderBy(x => x.TimeStart).ToList();
            });
        }

        public void ResetStatisticsDatabase()
        {
            Execute(db =>
            {
                db.DropCollection("statistics");
                db.Shrink();
            });
        }

        public void CleanOldValues(DateTime thresholdDate)
        {
            Execute(statistics => statistics.Delete(x => x.TimeStart < thresholdDate));
            Execute(db => db.Shrink());
        }

        public void AddStat(StatisticsDbEntry entry)
        {
            Execute(statistics => statistics.Insert(entry));
        }

        public void DeleteStatByDateAndValue(DateTime starTime, double value)
        {
            Execute(statistics => statistics.Delete(x => x.TimeStart == starTime && x.AvgValue == value));
        }
    }
}
