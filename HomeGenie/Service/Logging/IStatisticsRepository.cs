using System;
using System.Collections.Generic;
using System.Linq;

namespace HomeGenie.Service.Logging
{
    public interface IStatisticsRepository
    {
        StatisticsEntry GetDateRange();

        /// <summary>
        /// Gets the parameters list.
        /// </summary>
        /// <returns>The parameters list.</returns>
        /// <param name="domain">Domain.</param>
        /// <param name="address">Address.</param>
        List<string> GetParametersList(string domain, string address);

        /// <summary>
        /// Gets the total counter.
        /// </summary>
        /// <returns>The total counter.</returns>
        /// <param name="parameterName">Parameter name.</param>
        /// <param name="timeScaleSeconds">Time scale seconds.</param>
        double GetTotalCounter(string parameterName, double timeScaleSeconds);

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
        List<StatisticsEntry> GetHourlyCounter(
            string domain,
            string address,
            string parameterName,
            double timescaleseconds,
            DateTime startDate, DateTime endDate
        );

        List<IGrouping<StatGroup, StatisticsDbEntry>> GetGrouppedStats(
            string domain,
            string address,
            string parameterName,
            DateTime startDate, DateTime endDate
        );

        List<StatisticsDbEntry> GetStatsByParameter(
            string parameterName,
            DateTime startDate, DateTime endDate
        );

        List<StatisticsDbEntry> GetStatsByParameterAndDevice(
            string domain,
            string address,
            string parameterName,
            DateTime startDate, DateTime endDate
        );

        void ResetStatisticsDatabase();
        void CleanOldValues(DateTime thresholdDate);
        void AddStat(StatisticsDbEntry entry);
    }
}