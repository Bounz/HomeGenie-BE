using System;
using System.Collections.Generic;
using System.Linq;
using MIG;
using HomeGenie.Service.Logging;
using Newtonsoft.Json;

namespace HomeGenie.Service.Handlers
{
    public class Statistics
    {
        private HomeGenieService homegenie;

        public Statistics(HomeGenieService hg)
        {
            homegenie = hg;
        }

        public void ProcessRequest(MigClientRequest request)
        {
            var migCommand = request.Command;

            string response = "";
            string domain = "";
            string address = "";
            int domainSeparator = 0;
            DateTime dateStart, dateEnd;

            switch (migCommand.Command)
            {
            case "Global.CounterTotal":
                var counter = homegenie.Statistics.GetTotalCounter(migCommand.GetOption(0), 3600);
                request.ResponseData = new ResponseText(counter.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                break;

            case "Global.TimeRange":
                // TODO create dedicated class or use Tuple<DateTime, DateTime>
                var statEntry = homegenie.Statistics.GetDateRange();
                request.ResponseData = JsonConvert.SerializeObject(new
                {
                    StartTime = Utility.DateToJavascript(statEntry.TimeStart),
                    EndTime = Utility.DateToJavascript(statEntry.TimeEnd),
                });
                break;

            case "Database.Reset":
                homegenie.Statistics.ResetDatabase();
                break;
            case "Configuration.Get":
                // Just one at the moment.
                request.ResponseData = JsonConvert.SerializeObject(new
                {
                    StatisticsUIRefreshSeconds = homegenie.SystemConfiguration.HomeGenie.Statistics.StatisticsUIRefreshSeconds
                });
                break;
            case "Parameter.List":
                domainSeparator = migCommand.GetOption(0).LastIndexOf(":");
                if (domainSeparator > 0)
                {
                    domain = migCommand.GetOption(0).Substring(0, domainSeparator);
                    address = migCommand.GetOption(0).Substring(domainSeparator + 1);
                }
                var statParameters = homegenie.Statistics.GetParametersList(domain, address);
                response = JsonConvert.SerializeObject(statParameters);
                request.ResponseData = response;
                break;

            case "Parameter.Counter":
                domainSeparator = migCommand.GetOption(1).LastIndexOf(":");
                if (domainSeparator > 0)
                {
                    domain = migCommand.GetOption(1).Substring(0, domainSeparator);
                    address = migCommand.GetOption(1).Substring(domainSeparator + 1);
                }

                dateStart = Utility.JavascriptToDate(long.Parse(migCommand.GetOption(2)));
                dateEnd = Utility.JavascriptToDate(long.Parse(migCommand.GetOption(3)));
                var hoursAverage = homegenie.Statistics.GetHourlyCounter(domain, address, migCommand.GetOption(0), 3600, dateStart, dateEnd);

                response = JsonConvert.SerializeObject(hoursAverage);
                request.ResponseData = response;
                break;

            // [hourly MIN, hourly MAX, hourly AVG, today's SUM values for Meters or AVG values for everything else]
            case "Parameter.StatsHour":
                var hourlyStats = GetHourlyStats(migCommand);
                response = JsonConvert.SerializeObject(hourlyStats);
                request.ResponseData = response;
                break;

            // [detailed stats through days] // TODO rename this method to smth like GetDetailedStats
            case "Parameter.StatsDay":
                var dailyStats = GetDailyStats(migCommand);
                response = JsonConvert.SerializeObject(dailyStats);
                request.ResponseData = response;
                break;

            // [ [[stats], [moduleName]], [[stats], [moduleName]] ...]
            case "Parameter.StatsMultiple":
                var multipleModulesStats = GetMultipleModulesStats(migCommand);
                response = JsonConvert.SerializeObject(multipleModulesStats);
                request.ResponseData = response;
                break;

            case "Parameter.StatDelete":
                var dateText = migCommand.GetOption(0).Replace('.', ',');
                dateStart = Utility.JavascriptToDateUtc(double.Parse(dateText));
                var responseDelete = homegenie.Statistics.DeleteStat(dateStart, migCommand.GetOption(1));
                request.ResponseData = responseDelete;
                break;
            }
        }

        // TODO strong typing
        private object GetHourlyStats(MigInterfaceCommand migCommand)
        {
            var domain = "";
            var address = "";
            var domainSeparator = migCommand.GetOption(1).LastIndexOf(":");
            if (domainSeparator > 0)
            {
                domain = migCommand.GetOption(1).Substring(0, domainSeparator);
                address = migCommand.GetOption(1).Substring(domainSeparator + 1);
            }

            var dateStart = Utility.JavascriptToDate(long.Parse(migCommand.GetOption(2)));
            var dateEnd = Utility.JavascriptToDate(long.Parse(migCommand.GetOption(3)));
            var parameterName = migCommand.GetOption(0);
            var hourlyStats = homegenie.Statistics.GetHourlyStats(domain, address, parameterName, dateStart, dateEnd);
            var todayStartDate = DateTime.Today;
            var todayEndDate = todayStartDate.AddDays(1);
            var todayDetails = homegenie.Statistics.GetDetailedStats(domain, address, parameterName, todayStartDate, todayEndDate);
            return new object[]
            {
                hourlyStats[0].ToJsStatsArray(),
                hourlyStats[1].ToJsStatsArray(),
                hourlyStats[2].ToJsStatsArray(),
                todayDetails.ToJsStatsArray()
            };
        }

        // TODO strong typing
        private object GetDailyStats(MigInterfaceCommand migCommand)
        {
            var domain = "";
            var address = "";
            var domainSeparator = migCommand.GetOption(1).LastIndexOf(":");
            if (domainSeparator > 0)
            {
                domain = migCommand.GetOption(1).Substring(0, domainSeparator);
                address = migCommand.GetOption(1).Substring(domainSeparator + 1);
            }

            var dateStart = Utility.JavascriptToDate(long.Parse(migCommand.GetOption(2)));
            var dateEnd = Utility.JavascriptToDate(long.Parse(migCommand.GetOption(3)));
            var parameterName = migCommand.GetOption(0);
            var dailyStats = homegenie.Statistics.GetDetailedStats(domain, address, parameterName, dateStart, dateEnd);

            return dailyStats.ToJsStatsArray();
        }

        // TODO strong typing
        private List<ModuleStatsDto> GetMultipleModulesStats(MigInterfaceCommand migCommand)
        {
            var dateStart = Utility.JavascriptToDate(long.Parse(migCommand.GetOption(2)));
            var dateEnd = Utility.JavascriptToDate(long.Parse(migCommand.GetOption(3)));
            var parameterName = migCommand.GetOption(0);
            var dailyStats = homegenie.Statistics.GetMultipleModulesDetailedStats(parameterName, dateStart, dateEnd);

            var result = new List<ModuleStatsDto>();
            foreach (var moduleStats in dailyStats)
            {
                result.Add(new ModuleStatsDto
                {
                    Name = moduleStats.Key,
                    Stats = moduleStats.Value.ToJsStatsArray()
                });
            }
            return result;
        }
    }

    public static class StatsExtensions{

        public static double[][] ToJsStatsArray(this IEnumerable<StatGraphEntry> source)
        {
            return source.Select(x => new[] {x.Timestamp, x.Value}).ToArray();
        }
    }

    public class ModuleStatsDto
    {
        public string Name { get; set; }
        public double[][] Stats { get; set; }
    }
}
