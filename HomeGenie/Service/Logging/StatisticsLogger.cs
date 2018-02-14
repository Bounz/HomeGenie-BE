/*
    This file is part of HomeGenie Project source code.

    HomeGenie is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    HomeGenie is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with HomeGenie.  If not, see <http://www.gnu.org/licenses/>.  
*/

/*
 *     Author: Generoso Martello <gene@homegenie.it>
 *     Project Homepage: http://github.com/Bounz/HomeGenie-BE
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using System.IO;
using HomeGenie.Service.Constants;

namespace HomeGenie.Service.Logging
{
    public class StatisticsLogger
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

        private Timer _logInterval;
        private readonly HomeGenieService _homegenie;
        private long _dbSizeLimit = 2097152;

        private readonly int _statisticsTimeResolutionSeconds = 5 * 60;

        private readonly StatisticsRepository _statisticsRepository;

        public StatisticsLogger(HomeGenieService hg)
        {
            _homegenie = hg;
            _dbSizeLimit = hg.SystemConfiguration.HomeGenie.Statistics.MaxDatabaseSizeMBytes * 1024 * 1024;
            _statisticsTimeResolutionSeconds = hg.SystemConfiguration.HomeGenie.Statistics.StatisticsTimeResolutionSeconds;
            _statisticsRepository = new StatisticsRepository();
            _statisticsTimeResolutionSeconds = 30;
        }

        /// <summary>
        /// Start this instance.
        /// </summary>
        public void Start()
        {
            //OpenStatisticsDatabase();
            if (_logInterval == null)
            {

                _logInterval = new Timer(TimeSpan.FromSeconds(_statisticsTimeResolutionSeconds).TotalMilliseconds);
                _logInterval.Elapsed += logInterval_Elapsed;
            }
            _logInterval.Start();
        }

        /// <summary>
        /// Stop this instance.
        /// </summary>
        public void Stop()
        {
            if (_logInterval != null)
            {
                _logInterval.Elapsed -= logInterval_Elapsed;
                _logInterval.Stop();
                _logInterval.Dispose();
                _logInterval = null;
            }
            //CloseStatisticsDatabase();
        }

        /// <summary>
        /// Gets or sets the size limit.
        /// </summary>
        /// <value>The size limit.</value>
        public long SizeLimit
        {
            get { return _dbSizeLimit; }
            set { _dbSizeLimit = value; }
        }

        /// <summary>
        /// Resets the database.
        /// </summary>
        public void ResetDatabase()
        {
            ResetStatisticsDatabase();
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
        public List<StatisticsEntry> GetHourlyCounter(
            string domain,
            string address,
            string parameterName,
            double timescaleseconds,
            DateTime startDate, DateTime endDate
        )
        {
            return _statisticsRepository.GetHourlyCounter(domain, address, parameterName, timescaleseconds, startDate,
                endDate);
        }

        /// <summary>
        /// This is for the current day's AVERAGES part: (TODAY_AVG)
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="address"></param>
        /// <param name="parameterName"></param>
        /// <param name="aggregator"></param>
        /// <returns></returns>
        public List<StatisticsEntry> GetHourlyStatsToday(
            string domain,
            string address,
            string parameterName,
            string aggregator
        )
        {
            var values = new List<StatisticsEntry>();
//            var dbCommand = _dbConnection.CreateCommand();
//            var filter = "";
//            var start = DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " 00:00:00.000000");
//            dbCommand.Parameters.Add(new SqliteParameter("@start", start.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.ffffff") ));
//            //if (domain != "" && address != "") filter = "Domain ='" + domain + "' and Address = '" + address + "' and ";
//            if (!string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(address))
//            {
//                filter = " Domain=@domain AND Address=@address and ";
//                dbCommand.Parameters.Add(new SqliteParameter("@domain", domain));
//                dbCommand.Parameters.Add(new SqliteParameter("@address", address));
//            }
//            dbCommand.Parameters.Add(new SqliteParameter("@parameterName", parameterName));
//            // aggregated averages by hour
//            var query = "select TimeStart,TimeEnd,Domain,Address," + aggregator + "(AverageValue) as Value from ValuesHist where " + filter + "Parameter = @parameterName and TimeStart >= @start group by Domain, Address, strftime('%H', TimeStart)  order by TimeStart asc;";
//            dbCommand.CommandText = query;
//            var reader = dbCommand.ExecuteReader();
//            while (reader.Read())
//            {
//                var entry = new StatisticsEntry();
//                entry.TimeStart = DateTime.Parse(reader.GetString(0));
//                entry.TimeEnd = DateTime.Parse(reader.GetString(1));
//                entry.Domain = reader.GetString(2);
//                entry.Address = reader.GetString(3);
//                entry.Value = 0;
//                try
//                {
//                    entry.Value = (double)reader.GetFloat(4);
//                }
//                catch
//                {
//                    var value = reader.GetValue(4);
//                    if (value != DBNull.Value && value != null) double.TryParse(
//                            reader.GetString(4),
//                            out entry.Value
//                        );
//                }
//                values.Add(entry);
//            }
//            reader.Close();
            return values;
        }

        /// <summary>
        /// Gets the today detail.
        /// </summary>
        /// <returns>The today detail.</returns>
        /// <param name="domain">Domain.</param>
        /// <param name="address">Address.</param>
        /// <param name="parameterName">Parameter name.</param>
        /// <param name="aggregator">Aggregator.</param>
        public List<StatisticsEntry> GetTodayDetail(
            string domain,
            string address,
            string parameterName,
            string aggregator = "Avg"
        )
        {
            var values = new List<StatisticsEntry>();
//            var dbCommand = _dbConnection.CreateCommand();
//            var filter = "";
//            var groupBy = "";
//            var start = DateTime.Parse(DateTime.Now.ToString("yyyy-MM-dd") + " 00:00:00.000000");
//            dbCommand.Parameters.Add(new SqliteParameter("@start", start.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.ffffff")));
//
//            if (!string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(address))
//            {
//                // detailed module stats. We set our own aggregator. (Detailed red line in chart)
//                filter = " Domain=@domain AND Address=@address and ";
//                dbCommand.Parameters.Add(new SqliteParameter("@domain", domain));
//                dbCommand.Parameters.Add(new SqliteParameter("@address", address));
//                aggregator = "AverageValue";
//            }
//            else
//            {
//                // aggregated averages by hour
//                if (!string.IsNullOrEmpty(aggregator))
//                {
//                    aggregator = aggregator + "(AverageValue)";
//                }
//                groupBy = "group by TimeStart";
//            }
//            var query = "select TimeStart,TimeEnd,Domain,Address," + aggregator + " as Value from ValuesHist where " + filter + " Parameter = @parameterName AND TimeStart >= @start " + groupBy + " order by TimeStart asc;";
//            dbCommand.Parameters.Add(new SqliteParameter("@parameterName", parameterName));
//            dbCommand.CommandText = query;
//
//            var reader = dbCommand.ExecuteReader();
//            while (reader.Read())
//            {
//                // If nothing is found in filter during aggregate, we get a row of all DBNulls. Skip the entry.
//                // NOTE: We got an exception before this check if HG sends a request for a param that has no results
//                //       for the Parameter/TimeStart filter. We got single row of all DBNulls.
//                if (reader.IsDBNull(0))
//                {
//                    continue;
//                }
//                var entry = new StatisticsEntry();
//                entry.TimeStart = DateTime.Parse(reader.GetString(0));
//                entry.TimeEnd = DateTime.Parse(reader.GetString(1));
//                entry.Domain = reader.GetString(2);
//                entry.Address = reader.GetString(3);
//                entry.Value = 0;
//                try
//                {
//                    entry.Value = (double)reader.GetFloat(4);
//                }
//                catch
//                {
//                    var value = reader.GetValue(4);
//                    if (value != DBNull.Value && value != null) double.TryParse(
//                            reader.GetString(4),
//                            out entry.Value
//                        );
//                }
//                values.Add(entry);
//            }
//            reader.Close();

            return values;
        }

        /// <summary>
        /// This is for the overall AVERAGES part: (MIN), (MAX), (AVG)
        /// </summary>
        /// <param name="domain"></param>
        /// <param name="address"></param>
        /// <param name="parameterName"></param>
        /// <param name="aggregator"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public List<StatisticsEntry> GetHourlyStats(
            string domain,
            string address,
            string parameterName,
            string aggregator,
            DateTime startDate, DateTime endDate
        )
        {
            var values = new List<StatisticsEntry>();
//            var dbCommand = _dbConnection.CreateCommand();
//            var filter = "";
//
//            if (!string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(address))
//            {
//                filter = " Domain=@domain AND Address=@address and ";
//                dbCommand.Parameters.Add(new SqliteParameter("@domain", domain));
//                dbCommand.Parameters.Add(new SqliteParameter("@address", address));
//            }
//            var query = "";
//            if (aggregator != "")
//            {
//                if(aggregator == "All")
//                    query = "select TimeStart,TimeEnd,Domain,Address,CustomData,AverageValue from ValuesHist where " + filter + " Parameter = @parameterName AND " + GetParameterizedDateRangeFilter(ref dbCommand, startDate, endDate) + " order by CustomData, Domain, Address, TimeStart asc;";
//                else
//                    query = "select TimeStart,TimeEnd,Domain,Address,CustomData," + aggregator + "(AverageValue) as Value from ValuesHist where " + filter + " Parameter = @parameterName AND " + GetParameterizedDateRangeFilter(ref dbCommand, startDate, endDate) + " group by Domain, Address, strftime('%H', TimeStart)  order by TimeStart asc;";
//            }
//            else
//                query = "select TimeStart,TimeEnd,Domain,Address,CustomData,AverageValue from ValuesHist where " + filter + " Parameter = @parameterName AND " + GetParameterizedDateRangeFilter(ref dbCommand, startDate, endDate) + " order by TimeStart asc;";
//            dbCommand.Parameters.Add(new SqliteParameter("@parameterName", parameterName));
//
//            //if (domain != "" && address != "") filter = "Domain ='" + domain + "' and Address = '" + address + "' and ";
//            //string query = "select TimeStart,TimeEnd,Domain,Address," + aggregator + "(AverageValue) as Value from ValuesHist where " + filter + "Parameter = '" + parameterName + "' AND " + GetDateRangeFilter(startDate, endDate) + " group by Domain, Address, strftime('%H', TimeStart)  order by TimeStart asc;";
//            dbCommand.CommandText = query;
//            var reader = dbCommand.ExecuteReader();
//            //
//            while (reader.Read())
//            {
//                var entry = new StatisticsEntry();
//                entry.TimeStart = DateTime.Parse(reader.GetString(0));
//                entry.TimeEnd = DateTime.Parse(reader.GetString(1));
//                entry.Domain = reader.GetString(2);
//                entry.Address = reader.GetString(3);
//                entry.CustomData = reader.GetString(4);
//                entry.Value = 0;
//                try
//                {
//                    entry.Value = (double)reader.GetFloat(5);
//                }
//                catch
//                {
//                    var value = reader.GetValue(5);
//                    if (value != DBNull.Value && value != null) double.TryParse(
//                        reader.GetString(5),
//                        out entry.Value
//                    );
//                }
//                //
//                values.Add(entry);
//            }
//            //
//            reader.Close();
            return values;
        }

        /// <summary>
        /// Deletes the stat.
        /// </summary>
        /// <returns>The stat.</returns>
        /// <param name="startDate">Start date.</param>
        /// <param name="value">Value.</param>
        public string DeleteStat(
          DateTime startDate,
            string value
        )
        {
//            var valueText = "";
//            var fValue = double.Parse(value,CultureInfo.InvariantCulture);
//            var valueSplit = fValue.ToString("0.00", CultureInfo.InvariantCulture);
//            if (valueSplit == value)
//                valueText = " AND (AverageValue = '" + value + "')";
//            var dbCommand = _dbConnection.CreateCommand();
//            dbCommand.CommandText = "DELETE FROM ValuesHist WHERE (TimeStart BETWEEN '"+startDate.ToString("yyyy-MM-dd HH:mm:ss")+"' AND '"+startDate.ToString("yyyy-MM-dd HH:mm:ss")+".999999')"+valueText;
//            dbCommand.ExecuteNonQuery();
//            return dbCommand.CommandText;
            return "";
        }



        /// <summary>
        /// Resets the statistics database.
        /// </summary>
        private void ResetStatisticsDatabase()
        {
            _statisticsRepository.ResetStatisticsDatabase();
        }

        /// <summary>
        /// Gets the name of the statistics database.
        /// </summary>
        /// <returns>The statistics database name.</returns>
        private string GetStatisticsDatabaseName()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, StatisticsRepository.StatisticsDbFile);
        }

        private void logInterval_Elapsed(object sender, ElapsedEventArgs eventArgs)
        {
            var end = DateTime.UtcNow;
            var modules = _homegenie.Modules;
            for (var m = 0; m < modules.Count; m++)
            {
                var module = modules[m];
                for (var p = 0; p < module.Properties.Count; p++)
                {
                    var parameter = module.Properties[p];
                    if (parameter.Statistics.Values.Count > 0)
                    {
                        var values = parameter.Statistics.Values.FindAll(sv => (sv.Timestamp.Ticks <= end.Ticks && sv.Timestamp.Ticks > parameter.Statistics.LastProcessedTimestap.Ticks));
                        if (values.Count > 0)
                        {
                            var average = (values.Sum(d => d.Value) / values.Count);
                            //
                            //TODO: improve db file age/size check for archiving old data
                            //
                            try
                            {
                                var dbName = GetStatisticsDatabaseName();
                                var fileInfo = new FileInfo(dbName);
                                if (fileInfo.Length > _dbSizeLimit)
                                {
                                    // TODO: Test method below, then use that instead of rsetting whole database.
                                    CleanOldValuesFromStatisticsDatabase();
                                }

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
                                    "Database Error", 
                                    "Exception.StackTrace",
                                    $"{ex.Message}: {ex.StackTrace}"
                                );
                            }

                            parameter.Statistics.LastProcessedTimestap = end;
                            parameter.Statistics.Values.Clear();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Removes older values to keep DB size within configured size limit. Currently just cuts out last half of dates.
        /// </summary>
        private void CleanOldValuesFromStatisticsDatabase()
        {
            // + NUM_DAYS = Find number of days stored in DB.
            var stat = GetDateRange();
            var numDays = DateTime.Now.Subtract(stat.TimeStart).Days;
            var numDaysRemove = (int)Math.Floor(numDays / 2d);
            // + NUM_RECORDS = Get number of records.
            // + NUM_RECORDS_PER_DAY = Divide number of records by days to get records per day. (Not needed yet)

            // +++ We ultiumately want to shrink DB size by 50% or so...
            //     Just divide NUM_DAYS by 2. That should handle most cases.
            CleanOldValuesFromStatisticsDatabase(numDaysRemove);

        }

        /// <summary>
        /// Removes older values to keep DB size within configured size limit.
        /// </summary>
        /// <param name="numberOfDays">Records older than this number of days are removed.</param>
        private void CleanOldValuesFromStatisticsDatabase(int numberOfDays)
        {
            if (numberOfDays <= 0)
                return;

            var thresholdDate = DateTime.UtcNow.Date.AddDays(-numberOfDays);
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
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

    }
}
