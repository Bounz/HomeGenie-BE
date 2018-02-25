using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;

namespace HomeGenie.Service.Logging
{
    public class StatisticsRepository : IStatisticsRepository
    {
        public const string StatisticsDbFile = "homegenie_stats.litedb";

        public StatisticsRepository()
        {
            using (var db = new LiteDatabase(StatisticsDbFile))
            {
                var statistics = GetCollection(db);
                statistics.EnsureIndex(x => x.TimeStart);
                statistics.EnsureIndex(x => x.TimeEnd);
            }
        }

        public StatisticsEntry GetDateRange()
        {
            var start = DateTime.UtcNow;
            var end = DateTime.UtcNow;
            try
            {
                using (var db = new LiteDatabase(StatisticsDbFile))
                {
                    var statistics = GetCollection(db);
                    if(statistics.Count() == 0)
                        return new StatisticsEntry { TimeStart = start, TimeEnd = end };
                    start = statistics.Min(x => x.TimeStart);
                    end = statistics.Max(x => x.TimeEnd);
                }
            }
            catch (Exception ex)
            {
                ex = ex;
                // TODO: add error logging
            }

            return new StatisticsEntry { TimeStart = start, TimeEnd = end };
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
            using (var db = new LiteDatabase(StatisticsDbFile))
            {
                var statistics = GetCollection(db);
                var parameterList = (deviceSpecified
                        ? statistics.Find(x => x.Domain == domain && x.Address == address)
                        : statistics.FindAll()
                    ).Select(x => x.Parameter).Distinct().ToList();
                return parameterList;
            }
        }

        /// <summary>
        /// Gets the total counter.
        /// </summary>
        /// <returns>The total counter.</returns>
        /// <param name="parameterName">Parameter name.</param>
        /// <param name="timeScaleSeconds">Time scale seconds.</param>
        public double GetTotalCounter(string parameterName, double timeScaleSeconds)
        {
            double total = 0;
            try
            {
                using (var db = new LiteDatabase(StatisticsDbFile))
                {
                    var statistics = GetCollection(db);
                    var statItems = statistics.Find(x => x.Parameter == parameterName);
                    total = statItems.Sum(x => x.AvgValue * (x.TimeEnd - x.TimeStart).TotalSeconds / timeScaleSeconds);
                }

            } catch {
                // TODO: report/handle exception
            }
            return total;
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
        public List<StatisticsEntry> GetHourlyCounter(
            string domain,
            string address,
            string parameterName,
            double timescaleseconds,
            DateTime startDate, DateTime endDate
        )
        {
            var values = new List<StatisticsEntry>();
            using (var db = new LiteDatabase(StatisticsDbFile))
            {
                var statistics = GetCollection(db);
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
                    values.Add(new StatisticsEntry
                    {
                        Domain = grouppedStat.Key.Domain,
                        Address = grouppedStat.Key.Address,
                        TimeStart = startDate.Date.AddHours(grouppedStat.Key.Hour),
                        TimeEnd = grouppedStat.First().TimeEnd,
                        Value = grouppedStat.Sum(x => x.AvgValue * (x.TimeEnd - x.TimeStart).TotalSeconds / timescaleseconds)
                    });
                }
            }

            return values;
        }

        public List<IGrouping<StatGroup, StatisticsDbEntry>> GetGrouppedStats(
            string domain,
            string address,
            string parameterName,
            DateTime startDate, DateTime endDate
        )
        {
            using (var db = new LiteDatabase(StatisticsDbFile))
            {
                var statistics = GetCollection(db);
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
                    Domain = x.Domain, Address = x.Address, Hour = x.TimeStart.Hour
                });
                return grouppedStats.ToList();
            }
        }

        public List<StatisticsDbEntry> GetStatsByParameter(
            string parameterName,
            DateTime startDate, DateTime endDate
        )
        {
            using (var db = new LiteDatabase(StatisticsDbFile))
            {
                var statistics = GetCollection(db);
                var statItems = statistics.Find(x => x.Parameter == parameterName &&
                                                     x.TimeStart >= startDate &&
                                                     x.TimeEnd <= endDate);
                return statItems.OrderBy(x => x.TimeStart).ToList();
            }
        }

        public List<StatisticsDbEntry> GetStatsByParameterAndDevice(
            string domain,
            string address,
            string parameterName,
            DateTime startDate, DateTime endDate
        )
        {
            using (var db = new LiteDatabase(StatisticsDbFile))
            {
                var statistics = GetCollection(db);
                var statItems = statistics.Find(x => x.Parameter == parameterName &&
                                                     x.TimeStart >= startDate &&
                                                     x.TimeEnd <= endDate &&
                                                     x.Address == address &&
                                                     x.Domain == domain);
                return statItems.OrderBy(x => x.TimeStart).ToList();
            }
        }

        public void ResetStatisticsDatabase()
        {
            using (var db = new LiteDatabase(StatisticsDbFile))
            {
                db.DropCollection("statistics");
                db.Shrink();
            }
        }

        public void CleanOldValues(DateTime thresholdDate)
        {
            using (var db = new LiteDatabase(StatisticsDbFile))
            {
                var statistics = GetCollection(db);
                statistics.Delete(x => x.TimeStart < thresholdDate);
                db.Shrink();
            }
        }

        public void AddStat(StatisticsDbEntry entry)
        {
            using (var db = new LiteDatabase(StatisticsDbFile))
            {
                var statistics = GetCollection(db);
                statistics.Insert(entry);
            }
        }

        public void DeleteStatByDateAndValue(DateTime starTime, double value)
        {
            using (var db = new LiteDatabase(StatisticsDbFile))
            {
                var statistics = GetCollection(db);
                statistics.Delete(x => x.TimeStart == starTime && x.AvgValue == value);
            }
        }

        private LiteCollection<StatisticsDbEntry> GetCollection(LiteDatabase db)
        {
            return db.GetCollection<StatisticsDbEntry>("statistics");
        }
    }
}
