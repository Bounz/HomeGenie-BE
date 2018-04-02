using System;
using HomeGenie.Database;

namespace StatsGenerator
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            Generate();
        }

        private static void Generate()
        {
            var statisticsRepository = new StatisticsRepository();
            var fromDate = DateTime.Now.AddDays(-1);
            var rnd = new Random();

            for (int i = 0; i < 24*60/5; i++)
            {
                var dateStart = fromDate.AddMinutes(i * 5);
                var value = rnd.Next(150, 250) / 10.0;
                statisticsRepository.AddStat(new StatisticsDbEntry
                {
                    TimeStart = dateStart,
                    TimeEnd = dateStart.AddMinutes(5),
                    Domain = "HomeAutomation.BasicThermostat",
                    Address = "1",
                    Parameter = "Sensor.Temperature",
                    AvgValue = value,
                    ModuleName = "Thermostat"
                });
            }
        }
    }
}
