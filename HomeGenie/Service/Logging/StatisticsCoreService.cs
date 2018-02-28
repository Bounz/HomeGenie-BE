using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Timers;
using HomeGenie.Data;
using HomeGenie.Service.Constants;

namespace HomeGenie.Service.Logging
{
    public class StatisticsCoreService
    {
        public static List<string> StatisticsFields = new List<string>
        {
            "Sensor.",
            "Meter.",
            "PowerMonitor.",
            "Statistics."
        };

        public static bool IsValidField(string field)
        {
            var isValid = false;
            foreach (var f in StatisticsFields)
            {
                if (field.StartsWith(f))
                {
                    isValid = true;
                    break;
                }
            }
            return isValid;
        }

        private readonly Timer _logInterval;
        private readonly HomeGenieService _homegenie;
        private readonly IStatisticsRepository _statisticsRepository;
        private readonly IDateTime _dateTime;

        public StatisticsCoreService(HomeGenieService hg, IStatisticsRepository statisticsRepository, IDateTime dateTime)
        {
            _homegenie = hg;
            // TODO change default statistics resolution
            //var statisticsTimeResolutionSeconds = hg.SystemConfiguration.HomeGenie.Statistics.StatisticsTimeResolutionSeconds;
            var statisticsTimeResolutionSeconds = 30;
            _statisticsRepository = statisticsRepository;
            _dateTime = dateTime;

            _logInterval = new Timer(TimeSpan.FromSeconds(statisticsTimeResolutionSeconds).TotalMilliseconds);
            _logInterval.Elapsed += logInterval_Elapsed;
        }

        /// <summary>
        /// Start this instance.
        /// </summary>
        public void Start()
        {
            _logInterval.Start();
        }

        /// <summary>
        /// Stop this instance.
        /// </summary>
        public void Stop()
        {
            _logInterval?.Stop();
        }

        /// <summary>
        /// Resets the statistics database.
        /// </summary>
        public void ResetDatabase()
        {
            try
            {
                _statisticsRepository.ResetStatisticsDatabase();
            }
            catch (Exception ex)
            {
                HomeGenieService.LogError(
                    Domains.HomeAutomation_HomeGenie,
                    "Service.StatisticsLogger",
                    "Error resetting database",
                    "Exception.StackTrace",
                    $"{ex.Message}: {ex.StackTrace}"
                );
            }
        }

        /// <summary>
        /// Gets the parameters list.
        /// </summary>
        /// <returns>The parameters list.</returns>
        /// <param name="domain">Domain.</param>
        /// <param name="address">Address.</param>
        public List<string> GetParametersList(string domain, string address)
        {
            return _statisticsRepository.GetParametersList(domain, address);
        }

        /// <summary>
        /// Gets the date range.
        /// </summary>
        /// <returns>The date range.</returns>
        public StatisticsEntry GetDateRange()
        {
            return _statisticsRepository.GetDateRange();
        }

        /// <summary>
        /// Gets the total counter.
        /// </summary>
        /// <returns>The total counter.</returns>
        /// <param name="parameterName">Parameter name.</param>
        /// <param name="timeScaleSeconds">Time scale seconds.</param>
        public double GetTotalCounter(string parameterName, double timeScaleSeconds)
        {
            return _statisticsRepository.GetTotalCounter(parameterName, timeScaleSeconds);
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
        public object[][] GetHourlyCounter(//List<StatisticsEntry>
            string domain,
            string address,
            string parameterName,
            double timescaleseconds,
            DateTime startDate, DateTime endDate
        )
        {
            var hoursAverage = _statisticsRepository.GetHourlyCounter(domain, address, parameterName, timescaleseconds,
                startDate, endDate);

            var dayHourlyStats = new List<object>();

            for (int h = 0; h < 24; h++)
            {
                StatisticsEntry firstEntry = null;
                if (hoursAverage != null && hoursAverage.Count > 0)
                {
                    firstEntry = hoursAverage.Find(se => se.TimeStart.ToLocalTime().Hour == h);
                }
                var date = _dateTime.Today.AddHours(h);

                if (firstEntry != null)
                {
                    var sum = hoursAverage.FindAll(se => se.TimeStart.ToLocalTime().Hour == h).Sum(se => se.Value);
                    var item = new[] {Utility.DateToJavascript(date), sum};
                    dayHourlyStats.Add(item);
                }
                else
                {
                    var item = new[] {Utility.DateToJavascript(date), 0};
                    dayHourlyStats.Add(item);
                }
            }

            return new[] {dayHourlyStats.ToArray()};
        }

        /// <summary>
        /// Gets the today detail.
        /// </summary>
        /// <returns>The today detail.</returns>
        /// <param name="domain">Domain</param>
        /// <param name="address">Address</param>
        /// <param name="parameterName">Parameter name</param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        public List<StatGraphEntry> GetDetailedStats(
            string domain,
            string address,
            string parameterName,
            DateTime startDate, DateTime endDate
        )
        {
            var values = new List<StatGraphEntry>();

            /* several devices:
             *   - SUM for meters
             *   - AVG for sensors
             * specific device:
             *   - every stat
             */

            var deviceSpecified = !string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(address);
            var isMeterParameter = parameterName.StartsWith(Properties.MeterAny);
            var stats = deviceSpecified
                ? _statisticsRepository.GetStatsByParameterAndDevice(domain, address, parameterName, startDate, endDate)
                : _statisticsRepository.GetStatsByParameter(parameterName, startDate, endDate);

            if (deviceSpecified)
            {
                foreach (var stat in stats)
                {
                    values.Add(new StatGraphEntry {Timestamp = Utility.DateToJavascript(stat.TimeStart), Value = stat.AvgValue});
                }
                return values;
            }

            var statsByStartTimes = stats.GroupBy(x => x.TimeStart);
            foreach (var statsByStartTime in statsByStartTimes)
            {
                var jsDate = Utility.DateToJavascript(statsByStartTime.Key);
                values.Add(new StatGraphEntry {Timestamp = jsDate, Value = isMeterParameter
                    ? statsByStartTime.Sum(x => x.AvgValue)
                    : statsByStartTime.Average(x => x.AvgValue)
                });
            }

            return values;
        }

        /// <summary>
        /// This is for the overall AVERAGES part: (MIN), (MAX), (AVG)
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="address"></param>
        /// <param name="parameterName"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns>array of stats: hourly MIN[], hourly MAX[], hourly AVG[]</returns>
        public List<StatGraphEntry>[] GetHourlyStats(
            string domain,
            string address,
            string parameterName,
            DateTime startDate, DateTime endDate
        )
        {
            var deviceSpecified = !string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(address);
            var isMeterParameter = parameterName.StartsWith(Properties.MeterAny);

            var overallStats = deviceSpecified
                ? _statisticsRepository.GetStatsByParameterAndDevice(domain, address, parameterName, startDate, endDate)
                : _statisticsRepository.GetStatsByParameter(parameterName, startDate, endDate);

            var minValues = new List<StatGraphEntry>();
            var maxValues = new List<StatGraphEntry>();
            var avgValues = new List<StatGraphEntry>();

            for (int h = 0; h < 24; h++)
            {
                var date = _dateTime.Today.AddHours(h);
                var jsDate = Utility.DateToJavascript(date);
                var statsForHour = new List<StatisticsDbEntry>();
                if (isMeterParameter)
                {
                    var statsByModules = overallStats.Where(x => x.TimeStart.Hour == h).GroupBy(x => new {x.Domain, x.Address, x.TimeStart.Date}).ToList();
                    foreach (var statsByModule in statsByModules)
                        statsForHour.Add(new StatisticsDbEntry
                        {
                            Domain = statsByModule.Key.Domain,
                            Address = statsByModule.Key.Address,
                            AvgValue = statsByModule.Sum(x => x.AvgValue)
                        });
                }
                else
                {
                    statsForHour = overallStats.Where(x => x.TimeStart.Hour == h).ToList();
                }

                if (!statsForHour.Any())
                {
                    minValues.Add(new StatGraphEntry {Timestamp = jsDate, Value = 0});
                    maxValues.Add(new StatGraphEntry {Timestamp = jsDate, Value = 0});
                    avgValues.Add(new StatGraphEntry {Timestamp = jsDate, Value = 0});
                    continue;
                }

                minValues.Add(new StatGraphEntry {Timestamp = jsDate, Value = statsForHour.Min(x => x.AvgValue)});
                maxValues.Add(new StatGraphEntry {Timestamp = jsDate, Value = statsForHour.Max(x => x.AvgValue)});
                avgValues.Add(new StatGraphEntry {Timestamp = jsDate, Value = statsForHour.Average(x => x.AvgValue)});
            }

            return new[]
            {
                minValues,
                maxValues,
                avgValues
            };
        }

        /// <summary>
        /// This is for the overall AVERAGES part: (MIN), (MAX), (AVG)
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="address"></param>
        /// <param name="parameterName"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns>array of stats: daily MIN[], daily MAX[], daily AVG[]</returns>
        public List<StatGraphEntry>[] GetDailyStats(
            string domain,
            string address,
            string parameterName,
            DateTime startDate, DateTime endDate
        )
        {
            var deviceSpecified = !string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(address);
            var isMeterParameter = parameterName.StartsWith(Properties.MeterAny);

            var overallStats = deviceSpecified
                ? _statisticsRepository.GetStatsByParameterAndDevice(domain, address, parameterName, startDate, endDate)
                : _statisticsRepository.GetStatsByParameter(parameterName, startDate, endDate);

            var minValues = new List<StatGraphEntry>();
            var maxValues = new List<StatGraphEntry>();
            var avgValues = new List<StatGraphEntry>();

            var days = (endDate.Date - startDate.Date).TotalDays + 1;

            for (var d = 0; d < days; d++)
            {
                var date = startDate.Date.AddDays(d);
                var jsDate = Utility.DateToJavascript(date);
                var statsForDay = new List<StatisticsDbEntry>();
                if (isMeterParameter)
                {
                    var statsByModules = overallStats.Where(x => x.TimeStart.Date == date).GroupBy(x => new {x.Domain, x.Address}).ToList();
                    foreach (var statsByModule in statsByModules)
                        statsForDay.Add(new StatisticsDbEntry{AvgValue = statsByModule.Sum(x => x.AvgValue)});
                }
                else
                {
                    statsForDay = overallStats.Where(x => x.TimeStart.Date == date).ToList();
                }

                if (!statsForDay.Any())
                {
                    minValues.Add(new StatGraphEntry {Timestamp = jsDate, Value = 0});
                    maxValues.Add(new StatGraphEntry {Timestamp = jsDate, Value = 0});
                    avgValues.Add(new StatGraphEntry {Timestamp = jsDate, Value = 0});
                    continue;
                }

                minValues.Add(new StatGraphEntry {Timestamp = jsDate, Value = statsForDay.Min(x => x.AvgValue)});
                maxValues.Add(new StatGraphEntry {Timestamp = jsDate, Value = statsForDay.Max(x => x.AvgValue)});
                avgValues.Add(new StatGraphEntry {Timestamp = jsDate, Value = statsForDay.Average(x => x.AvgValue)});
            }

            return new[]
            {
                minValues,
                maxValues,
                avgValues
            };
        }

        public Dictionary<string, List<StatGraphEntry>> GetMultipleModulesDetailedStats(
            string parameterName,
            DateTime startDate, DateTime endDate)
        {
            try
            {
                var allModulesStats = _statisticsRepository.GetStatsByParameter(parameterName, startDate, endDate);
                var result = allModulesStats.GroupBy(x => string.IsNullOrEmpty(x.ModuleName) ? $"{x.Domain}.{x.Address}" : x.ModuleName).ToDictionary(
                    x => x.Key,
                    x => x.Select(stat => new StatGraphEntry {Timestamp = Utility.DateToJavascript(stat.TimeStart), Value = stat.AvgValue}).ToList());
                return result;
            }
            catch (Exception ex)
            {
                HomeGenieService.LogError(
                    Domains.HomeAutomation_HomeGenie,
                    "Service.StatisticsLogger",
                    "Error getting multiple modules detailed stats from the database",
                    "Exception.StackTrace",
                    $"{ex.Message}: {ex.StackTrace}"
                );
                return null;
            }

        }

        /// <summary>
        /// Deletes the stat.
        /// </summary>
        /// <returns>The stat.</returns>
        /// <param name="startDate">Start date.</param>
        /// <param name="value">Value.</param>
        public bool DeleteStat(DateTime startDate, string value)
        {
            try
            {
                var doubleValue = Convert.ToDouble(value.Replace(',', '.'), CultureInfo.InvariantCulture);
                _statisticsRepository.DeleteStatByDateAndValue(startDate, doubleValue);
                return true;
            }
            catch (Exception ex)
            {
                HomeGenieService.LogError(
                    Domains.HomeAutomation_HomeGenie,
                    "Service.StatisticsLogger",
                    "Error deleting stat from the database",
                    "Exception.StackTrace",
                    $"{ex.Message}: {ex.StackTrace}"
                );
                return false;
            }
        }

        private void logInterval_Elapsed(object sender, ElapsedEventArgs eventArgs)
        {
            CleanOldValuesFromStatisticsDatabase();

            var end = _dateTime.UtcNow;

            foreach (var module in _homegenie.Modules)
            {
                foreach (var parameter in module.Properties)
                {
                    var values = parameter.Statistics.Values.FindAll(sv => sv.Timestamp <= end &&
                                                                           sv.Timestamp > parameter.Statistics.LastProcessedTimestap);
                    if (!values.Any())
                        continue;

                    AddStatToDb(values, module, parameter, end);

                    parameter.Statistics.LastProcessedTimestap = end;
                    parameter.Statistics.Values.Clear();
                }
            }
        }

        private void AddStatToDb(IReadOnlyCollection<ValueStatistics.StatValue> values, Module module, ModuleParameter parameter, DateTime end)
        {
            var average = values.Sum(d => d.Value) / values.Count;
            try
            {
                _statisticsRepository.AddStat(new StatisticsDbEntry
                {
                    TimeStart = parameter.Statistics.LastProcessedTimestap,
                    TimeEnd = end,
                    Domain = module.Domain,
                    Address = module.Address,
                    Parameter = parameter.Name,
                    AvgValue = average,
                    ModuleName = module.Name
                });
            }
            catch (Exception ex)
            {
                HomeGenieService.LogError(
                    Domains.HomeAutomation_HomeGenie,
                    "Service.StatisticsLogger",
                    "Error adding stats to the database",
                    "Exception.StackTrace",
                    $"{ex.Message}: {ex.StackTrace}"
                );
            }
        }

        /// <summary>
        /// Removes older values to keep DB size within configured size limit. Currently just cuts out last half of dates.
        /// </summary>
        private void CleanOldValuesFromStatisticsDatabase()
        {
            const int daysToKeep = 7;
            CleanOldValuesFromStatisticsDatabase(daysToKeep);
        }

        /// <summary>
        /// Removes older values to keep DB size within configured size limit.
        /// </summary>
        /// <param name="daysToKeep">Records older than this number of days are removed.</param>
        private void CleanOldValuesFromStatisticsDatabase(int daysToKeep)
        {
            if (daysToKeep <= 0)
                return;

            var thresholdDate = _dateTime.UtcNow.Date.AddDays(-daysToKeep);
            try
            {
                _statisticsRepository.CleanOldValues(thresholdDate);

                HomeGenieService.LogDebug(
                    Domains.HomeAutomation_HomeGenie,
                    "Service.StatisticsLogger",
                    "Cleaned old values from database.",
                    "DayThreshold",
                    thresholdDate.ToString("O")
                );
            }
            catch (Exception ex)
            {
                HomeGenieService.LogError(
                    Domains.HomeAutomation_HomeGenie,
                    "Service.StatisticsLogger",
                    "Error cleaning old stats from the database",
                    "Exception.StackTrace",
                    $"{ex.Message}: {ex.StackTrace}"
                );
            }
        }

    }
}
