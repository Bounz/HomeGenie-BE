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
        private readonly HomeGenieService _homegenie;

        public Statistics(HomeGenieService hg)
        {
            _homegenie = hg;
        }

        public void ProcessRequest(MigClientRequest request)
        {
            var migCommand = request.Command;

            string response;
            var domain = "";
            var address = "";
            DateTime dateStart;

            var deviceAddress = migCommand.GetOption(0).Split(':');
            if(deviceAddress.Length == 2)
            {
                domain = deviceAddress[0];
                address = deviceAddress[1];
            }

            switch (migCommand.Command)
            {
                case "Global.CounterTotal":
                    var counter = _homegenie.Statistics.GetTotalCounter(migCommand.GetOption(0), 3600);
                    request.ResponseData = new ResponseText(counter.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                    break;

                case "Global.TimeRange":
                    // TODO create dedicated class or use Tuple<DateTime, DateTime>
                    var dateRange = _homegenie.Statistics.GetDateRange();
                    request.ResponseData = JsonConvert.SerializeObject(new
                    {
                        StartTime = Utility.DateToJavascript(dateRange.start),
                        EndTime = Utility.DateToJavascript(dateRange.end),
                    });
                    break;

                case "Database.Reset":
                    _homegenie.Statistics.ResetDatabase();
                    break;
                case "Configuration.Get":
                    // Just one at the moment.
                    request.ResponseData = JsonConvert.SerializeObject(new
                    {
                        StatisticsUIRefreshSeconds = _homegenie.SystemConfiguration.HomeGenie.Statistics.StatisticsUIRefreshSeconds
                    });
                    break;
                case "Parameter.List":
                    if(deviceAddress.Length == 2)
                    {
                        domain = deviceAddress[0];
                        address = deviceAddress[1];
                    }

                    var statParameters = _homegenie.Statistics.GetParametersList(domain, address);
                    response = JsonConvert.SerializeObject(statParameters);
                    request.ResponseData = response;
                    break;

                case "Parameter.Counter":
                    if(deviceAddress.Length == 2)
                    {
                        domain = deviceAddress[0];
                        address = deviceAddress[1];
                    }

                    dateStart = Utility.JavascriptToDate(long.Parse(migCommand.GetOption(2)));
                    var dateEnd = Utility.JavascriptToDate(long.Parse(migCommand.GetOption(3)));
                    var hoursAverage = _homegenie.Statistics.GetHourlyCounter(domain, address, migCommand.GetOption(0), 3600, dateStart, dateEnd);

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
                    var responseDelete = _homegenie.Statistics.DeleteStat(dateStart, migCommand.GetOption(1));
                    request.ResponseData = responseDelete;
                    break;
            }
        }

        // TODO strong typing
        private object GetHourlyStats(MigInterfaceCommand migCommand)
        {
            var domain = "";
            var address = "";
            var deviceAddress = migCommand.GetOption(0).Split(':');
            if(deviceAddress.Length == 2)
            {
                domain = deviceAddress[0];
                address = deviceAddress[1];
            }

            var dateStart = Utility.JavascriptToDate(long.Parse(migCommand.GetOption(2)));
            var dateEnd = Utility.JavascriptToDate(long.Parse(migCommand.GetOption(3)));
            var parameterName = migCommand.GetOption(0);
            var hourlyStats = _homegenie.Statistics.GetHourlyStats(domain, address, parameterName, dateStart, dateEnd);
            var todayStartDate = DateTime.Today;
            var todayEndDate = todayStartDate.AddDays(1);
            var todayDetails = _homegenie.Statistics.GetDetailedStats(domain, address, parameterName, todayStartDate, todayEndDate);
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
            var deviceAddress = migCommand.GetOption(0).Split(':');
            if(deviceAddress.Length == 2)
            {
                domain = deviceAddress[0];
                address = deviceAddress[1];
            }

            var dateStart = Utility.JavascriptToDate(long.Parse(migCommand.GetOption(2)));
            var dateEnd = Utility.JavascriptToDate(long.Parse(migCommand.GetOption(3)));
            var parameterName = migCommand.GetOption(0);
            var dailyStats = _homegenie.Statistics.GetDetailedStats(domain, address, parameterName, dateStart, dateEnd);

            return dailyStats.ToJsStatsArray();
        }

        // TODO strong typing
        private List<ModuleStatsDto> GetMultipleModulesStats(MigInterfaceCommand migCommand)
        {
            var dateStart = Utility.JavascriptToDate(long.Parse(migCommand.GetOption(2)));
            var dateEnd = Utility.JavascriptToDate(long.Parse(migCommand.GetOption(3)));
            var parameterName = migCommand.GetOption(0);
            var dailyStats = _homegenie.Statistics.GetMultipleModulesDetailedStats(parameterName, dateStart, dateEnd);

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
