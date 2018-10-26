using System;
using System.Collections.Generic;
using HomeGenie.Database;
using HomeGenie.Service;
using HomeGenie.Service.Logging;
using Moq;
using NUnit.Framework;

namespace HomeGenieTests
{
    [TestFixture]
    public class StatisticsCoreServiceTests
    {
        private const string Domain = "Domain";
        private const string Address = "Address";
        private const string MeterParameter = "Meter.Watts";
        private const string SensorParameter = "Sensor.Temperature";
        private static DateTime Today = new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static DateTime Now = new DateTime(2018, 1, 1, 3, 0, 0, DateTimeKind.Utc);
        private const long TodayMs = 1514764800000;

        private readonly Mock<IDateTime> _dateTime = new Mock<IDateTime>();

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _dateTime.Setup(x => x.Today).Returns(Today);
            _dateTime.Setup(x => x.Now).Returns(Now);
            _dateTime.Setup(x => x.UtcNow).Returns(Now.ToUniversalTime);
        }

        [Test]
        public void GetDetailedStats_MeterModuleForToday_CorrectValues()
        {
            // Arrange
            var statisticsRepository = new Mock<IStatisticsRepository>();
            statisticsRepository
                .Setup(x => x.GetStatsByParameterAndDevice(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns(new List<StatisticsDbEntry>
                {
                    NewMeterStatisticsDbEntry(Today.AddHours(1).AddMinutes(0), Today.AddHours(1).AddMinutes(5), 1),
                    NewMeterStatisticsDbEntry(Today.AddHours(1).AddMinutes(5), Today.AddHours(1).AddMinutes(10), 2),
                    NewMeterStatisticsDbEntry(Today.AddHours(1).AddMinutes(10), Today.AddHours(1).AddMinutes(15), 3),
                    NewMeterStatisticsDbEntry(Today.AddHours(2).AddMinutes(0), Today.AddHours(2).AddMinutes(5), 4),
                });

            // Act
            var service = new StatisticsCoreService(null, statisticsRepository.Object, _dateTime.Object);
            var result = service.GetDetailedStats(Domain, Address, MeterParameter, Today, Today.AddDays(1));

            // Assert
            Assert.That(result.Count, Is.EqualTo(4));

            Assert.That(result[0].Timestamp, Is.EqualTo(TodayMs + 1 * 3600 * 1000));
            Assert.That(result[0].Value, Is.EqualTo(1));

            Assert.That(result[1].Timestamp, Is.EqualTo(TodayMs + 1 * 3600 * 1000 + 5 * 60 * 1000));
            Assert.That(result[1].Value, Is.EqualTo(2));

            Assert.That(result[2].Timestamp, Is.EqualTo(TodayMs + 1 * 3600 * 1000 + 10 * 60 * 1000));
            Assert.That(result[2].Value, Is.EqualTo(3));

            Assert.That(result[3].Timestamp, Is.EqualTo(TodayMs + 2 * 3600 * 1000));
            Assert.That(result[3].Value, Is.EqualTo(4));
        }

        [Test]
        public void GetDetailedStats_SeveralMeterModulesForToday_CorrectValues()
        {
            // Arrange
            var statisticsRepository = new Mock<IStatisticsRepository>();
            statisticsRepository
                .Setup(x => x.GetStatsByParameter(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns(new List<StatisticsDbEntry>
                {
                    NewMeterStatisticsDbEntry(Today.AddHours(1).AddMinutes(0), Today.AddHours(1).AddMinutes(5), 50),
                    NewMeterStatisticsDbEntryM2(Today.AddHours(1).AddMinutes(0), Today.AddHours(1).AddMinutes(5), 10),

                    NewMeterStatisticsDbEntry(Today.AddHours(1).AddMinutes(5), Today.AddHours(1).AddMinutes(10), 40),
                    NewMeterStatisticsDbEntryM2(Today.AddHours(1).AddMinutes(5), Today.AddHours(1).AddMinutes(10), 10),

                    NewMeterStatisticsDbEntry(Today.AddHours(1).AddMinutes(10), Today.AddHours(1).AddMinutes(15), 30),
                    NewMeterStatisticsDbEntryM2(Today.AddHours(1).AddMinutes(10), Today.AddHours(1).AddMinutes(15), 10)
                });

            // Act
            var service = new StatisticsCoreService(null, statisticsRepository.Object, _dateTime.Object);
            var result = service.GetDetailedStats(null, null, MeterParameter, Today, Today.AddDays(1));

            // Assert
            Assert.That(result.Count, Is.EqualTo(3));

            Assert.That(result[0].Timestamp, Is.EqualTo(TodayMs + 1 * 3600 * 1000));
            Assert.That(result[0].Value, Is.EqualTo(60));

            Assert.That(result[1].Timestamp, Is.EqualTo(TodayMs + 1 * 3600 * 1000 + 5 * 60 * 1000));
            Assert.That(result[1].Value, Is.EqualTo(50));

            Assert.That(result[2].Timestamp, Is.EqualTo(TodayMs + 1 * 3600 * 1000 + 10 * 60 * 1000));
            Assert.That(result[2].Value, Is.EqualTo(40));
        }

        [Test]
        public void GetHourlyStats_SeveralMeterModulesOneDay_CorrectValues()
        {
            // Arrange
            var statisticsRepository = new Mock<IStatisticsRepository>();
            statisticsRepository
                .Setup(x => x.GetStatsByParameter(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns(new List<StatisticsDbEntry>
                {
                    NewMeterStatisticsDbEntry(Today.AddHours(1).AddMinutes(0), Today.AddHours(1).AddMinutes(5), 50),
                    NewMeterStatisticsDbEntry(Today.AddHours(1).AddMinutes(5), Today.AddHours(1).AddMinutes(10), 50),
                    NewMeterStatisticsDbEntryM2(Today.AddHours(1).AddMinutes(0), Today.AddHours(1).AddMinutes(5), 10),
                    NewMeterStatisticsDbEntryM2(Today.AddHours(1).AddMinutes(5), Today.AddHours(1).AddMinutes(10), 10),

                    NewMeterStatisticsDbEntry(Today.AddHours(2).AddMinutes(0), Today.AddHours(2).AddMinutes(5), 50),
                    NewMeterStatisticsDbEntry(Today.AddHours(2).AddMinutes(5), Today.AddHours(2).AddMinutes(10), 40),
                    NewMeterStatisticsDbEntryM2(Today.AddHours(2).AddMinutes(0), Today.AddHours(2).AddMinutes(5), 10),
                    NewMeterStatisticsDbEntryM2(Today.AddHours(2).AddMinutes(5), Today.AddHours(2).AddMinutes(10), 30)
                });

            // Act
            var service = new StatisticsCoreService(null, statisticsRepository.Object, _dateTime.Object);
            var result = service.GetHourlyStats(null, null, MeterParameter, Today, Today.AddDays(1));

            // Assert
            Assert.That(result.Length, Is.EqualTo(3));

            var minValues = result[0];
            Assert.That(minValues[0].Timestamp, Is.EqualTo(TodayMs));
            Assert.That(minValues[0].Value, Is.EqualTo(0));
            Assert.That(minValues[1].Timestamp, Is.EqualTo(TodayMs + 1 * 3600 * 1000));
            Assert.That(minValues[1].Value, Is.EqualTo(20));
            Assert.That(minValues[2].Timestamp, Is.EqualTo(TodayMs + 2 * 3600 * 1000));
            Assert.That(minValues[2].Value, Is.EqualTo(40));

            var maxValues = result[1];
            Assert.That(maxValues[0].Timestamp, Is.EqualTo(TodayMs));
            Assert.That(maxValues[0].Value, Is.EqualTo(0));
            Assert.That(maxValues[1].Timestamp, Is.EqualTo(TodayMs + 1 * 3600 * 1000));
            Assert.That(maxValues[1].Value, Is.EqualTo(100));
            Assert.That(maxValues[2].Timestamp, Is.EqualTo(TodayMs + 2 * 3600 * 1000));
            Assert.That(maxValues[2].Value, Is.EqualTo(90));

            var avgValues = result[2];
            Assert.That(avgValues[0].Timestamp, Is.EqualTo(TodayMs));
            Assert.That(avgValues[0].Value, Is.EqualTo(0));
            Assert.That(avgValues[1].Timestamp, Is.EqualTo(TodayMs + 1 * 3600 * 1000));
            Assert.That(avgValues[1].Value, Is.EqualTo(60));
            Assert.That(avgValues[2].Timestamp, Is.EqualTo(TodayMs + 2 * 3600 * 1000));
            Assert.That(avgValues[2].Value, Is.EqualTo(65));
        }

        [Test]
        public void GetHourlyStats_SeveralMeterModulesTwoDays_CorrectValues()
        {
            // Arrange
            var statisticsRepository = new Mock<IStatisticsRepository>();
            statisticsRepository
                .Setup(x => x.GetStatsByParameter(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns(new List<StatisticsDbEntry>
                {
                    NewMeterStatisticsDbEntry(Today.AddHours(1).AddMinutes(0), Today.AddHours(1).AddMinutes(5), 50),
                    NewMeterStatisticsDbEntry(Today.AddHours(1).AddMinutes(5), Today.AddHours(1).AddMinutes(10), 30),
                    NewMeterStatisticsDbEntryM2(Today.AddHours(1).AddMinutes(0), Today.AddHours(1).AddMinutes(5), 10),
                    NewMeterStatisticsDbEntryM2(Today.AddHours(1).AddMinutes(5), Today.AddHours(1).AddMinutes(10), 10),

                    NewMeterStatisticsDbEntry(Today.AddDays(1).AddHours(1).AddMinutes(0), Today.AddDays(1).AddHours(1).AddMinutes(5), 50),
                    NewMeterStatisticsDbEntry(Today.AddDays(1).AddHours(1).AddMinutes(5), Today.AddDays(1).AddHours(1).AddMinutes(10), 40),
                    NewMeterStatisticsDbEntryM2(Today.AddDays(1).AddHours(1).AddMinutes(0), Today.AddDays(1).AddHours(1).AddMinutes(5), 10),
                    NewMeterStatisticsDbEntryM2(Today.AddDays(1).AddHours(1).AddMinutes(5), Today.AddDays(1).AddHours(1).AddMinutes(10), 30)
                });

            // Act
            var service = new StatisticsCoreService(null, statisticsRepository.Object, _dateTime.Object);
            var result = service.GetHourlyStats(null, null, MeterParameter, Today.AddDays(-1), Today.AddDays(1));

            // Assert
            Assert.That(result.Length, Is.EqualTo(3));

            var minValues = result[0];
            Assert.That(minValues[0].Timestamp, Is.EqualTo(TodayMs));
            Assert.That(minValues[0].Value, Is.EqualTo(0));
            Assert.That(minValues[1].Timestamp, Is.EqualTo(TodayMs + 1 * 3600 * 1000));
            Assert.That(minValues[1].Value, Is.EqualTo(20));

            var maxValues = result[1];
            Assert.That(maxValues[0].Timestamp, Is.EqualTo(TodayMs));
            Assert.That(maxValues[0].Value, Is.EqualTo(0));
            Assert.That(maxValues[1].Timestamp, Is.EqualTo(TodayMs + 1 * 3600 * 1000));
            Assert.That(maxValues[1].Value, Is.EqualTo(90));

            var avgValues = result[2];
            Assert.That(avgValues[0].Timestamp, Is.EqualTo(TodayMs));
            Assert.That(avgValues[0].Value, Is.EqualTo(0));
            Assert.That(avgValues[1].Timestamp, Is.EqualTo(TodayMs + 1 * 3600 * 1000));
            Assert.That(avgValues[1].Value, Is.EqualTo(57.5));
        }

        [Test]
        public void GetHourlyStats_SeveralSensorModulesOneDay_CorrectValues()
        {
            // Arrange
            var statisticsRepository = new Mock<IStatisticsRepository>();
            statisticsRepository
                .Setup(x => x.GetStatsByParameter(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns(new List<StatisticsDbEntry>
                {
                    NewSensorStatisticsDbEntry(Today.AddHours(1).AddMinutes(0), Today.AddHours(1).AddMinutes(5), 27),
                    NewSensorStatisticsDbEntry(Today.AddHours(1).AddMinutes(5), Today.AddHours(1).AddMinutes(10), 28.54),
                    NewSensorStatisticsDbEntryM2(Today.AddHours(1).AddMinutes(0), Today.AddHours(1).AddMinutes(5), 12.5),
                    NewSensorStatisticsDbEntryM2(Today.AddHours(1).AddMinutes(5), Today.AddHours(1).AddMinutes(10), 11.3),

                    NewSensorStatisticsDbEntry(Today.AddHours(2).AddMinutes(0), Today.AddHours(2).AddMinutes(5), 29),
                    NewSensorStatisticsDbEntry(Today.AddHours(2).AddMinutes(5), Today.AddHours(2).AddMinutes(10), 25),
                    NewSensorStatisticsDbEntryM2(Today.AddHours(2).AddMinutes(0), Today.AddHours(2).AddMinutes(5), 12),
                    NewSensorStatisticsDbEntryM2(Today.AddHours(2).AddMinutes(5), Today.AddHours(2).AddMinutes(10), 14)
                });

            // Act
            var service = new StatisticsCoreService(null, statisticsRepository.Object, _dateTime.Object);
            var result = service.GetHourlyStats(null, null, SensorParameter, Today, Today.AddDays(1));

            // Assert
            Assert.That(result.Length, Is.EqualTo(3));

            var minValues = result[0];
            Assert.That(minValues[0].Timestamp, Is.EqualTo(TodayMs));
            Assert.That(minValues[0].Value, Is.EqualTo(0));
            Assert.That(minValues[1].Timestamp, Is.EqualTo(TodayMs + 1 * 3600 * 1000));
            Assert.That(minValues[1].Value, Is.EqualTo(11.3));
            Assert.That(minValues[2].Timestamp, Is.EqualTo(TodayMs + 2 * 3600 * 1000));
            Assert.That(minValues[2].Value, Is.EqualTo(12));

            var maxValues = result[1];
            Assert.That(maxValues[0].Timestamp, Is.EqualTo(TodayMs));
            Assert.That(maxValues[0].Value, Is.EqualTo(0));
            Assert.That(maxValues[1].Timestamp, Is.EqualTo(TodayMs + 1 * 3600 * 1000));
            Assert.That(maxValues[1].Value, Is.EqualTo(28.54));
            Assert.That(maxValues[2].Timestamp, Is.EqualTo(TodayMs + 2 * 3600 * 1000));
            Assert.That(maxValues[2].Value, Is.EqualTo(29));

            var avgValues = result[2];
            Assert.That(avgValues[0].Timestamp, Is.EqualTo(TodayMs));
            Assert.That(avgValues[0].Value, Is.EqualTo(0));
            Assert.That(avgValues[1].Timestamp, Is.EqualTo(TodayMs + 1 * 3600 * 1000));
            Assert.That(avgValues[1].Value, Is.EqualTo(19.835).Within(0.0001));
            Assert.That(avgValues[2].Timestamp, Is.EqualTo(TodayMs + 2 * 3600 * 1000));
            Assert.That(avgValues[2].Value, Is.EqualTo(20).Within(0.0001));
        }

        [Test]
        public void GetHourlyStats_SpecificMeterModuleTwoDays_CorrectValues()
        {
            // Arrange
            var statisticsRepository = new Mock<IStatisticsRepository>();
            statisticsRepository
                .Setup(x => x.GetStatsByParameterAndDevice(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns(new List<StatisticsDbEntry>
                {
                    NewMeterStatisticsDbEntry(Today.AddHours(1).AddMinutes(0), Today.AddHours(1).AddMinutes(5), 50),
                    NewMeterStatisticsDbEntry(Today.AddDays(1).AddHours(1).AddMinutes(5), Today.AddDays(1).AddHours(1).AddMinutes(10), 50),

                    NewMeterStatisticsDbEntry(Today.AddHours(2).AddMinutes(0), Today.AddHours(2).AddMinutes(5), 50),
                    NewMeterStatisticsDbEntry(Today.AddDays(1).AddHours(2).AddMinutes(5), Today.AddDays(1).AddHours(2).AddMinutes(10), 40)
                });

            // Act
            var service = new StatisticsCoreService(null, statisticsRepository.Object, _dateTime.Object);
            var result = service.GetHourlyStats(Domain, Address, MeterParameter, Today, Today.AddDays(1));

            // Assert
            Assert.That(result.Length, Is.EqualTo(3));

            var minValues = result[0];
            Assert.That(minValues[0].Timestamp, Is.EqualTo(TodayMs));
            Assert.That(minValues[0].Value, Is.EqualTo(0));
            Assert.That(minValues[1].Timestamp, Is.EqualTo(TodayMs + 1 * 3600 * 1000));
            Assert.That(minValues[1].Value, Is.EqualTo(50));
            Assert.That(minValues[2].Timestamp, Is.EqualTo(TodayMs + 2 * 3600 * 1000));
            Assert.That(minValues[2].Value, Is.EqualTo(40));

            var maxValues = result[1];
            Assert.That(maxValues[0].Timestamp, Is.EqualTo(TodayMs));
            Assert.That(maxValues[0].Value, Is.EqualTo(0));
            Assert.That(maxValues[1].Timestamp, Is.EqualTo(TodayMs + 1 * 3600 * 1000));
            Assert.That(maxValues[1].Value, Is.EqualTo(50));
            Assert.That(maxValues[2].Timestamp, Is.EqualTo(TodayMs + 2 * 3600 * 1000));
            Assert.That(maxValues[2].Value, Is.EqualTo(50));

            var avgValues = result[2];
            Assert.That(avgValues[0].Timestamp, Is.EqualTo(TodayMs));
            Assert.That(avgValues[0].Value, Is.EqualTo(0));
            Assert.That(avgValues[1].Timestamp, Is.EqualTo(TodayMs + 1 * 3600 * 1000));
            Assert.That(avgValues[1].Value, Is.EqualTo(50));
            Assert.That(avgValues[2].Timestamp, Is.EqualTo(TodayMs + 2 * 3600 * 1000));
            Assert.That(avgValues[2].Value, Is.EqualTo(45));
        }

        [Test]
        public void GetHourlyStats_SpecificSensorModuleTwoDays_CorrectValues()
        {
            // Arrange
            var statisticsRepository = new Mock<IStatisticsRepository>();
            statisticsRepository
                .Setup(x => x.GetStatsByParameterAndDevice(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .Returns(new List<StatisticsDbEntry>
                {
                    NewSensorStatisticsDbEntry(Today.AddHours(1).AddMinutes(0), Today.AddHours(1).AddMinutes(5), 27),
                    NewSensorStatisticsDbEntry(Today.AddDays(1).AddHours(1).AddMinutes(5), Today.AddDays(1).AddHours(1).AddMinutes(10), 25),

                    NewSensorStatisticsDbEntry(Today.AddHours(2).AddMinutes(0), Today.AddHours(2).AddMinutes(5), 29),
                    NewSensorStatisticsDbEntry(Today.AddDays(1).AddHours(2).AddMinutes(5), Today.AddDays(1).AddHours(2).AddMinutes(10), 25),
                });

            // Act
            var service = new StatisticsCoreService(null, statisticsRepository.Object, _dateTime.Object);
            var result = service.GetHourlyStats(Domain, Address, SensorParameter, Today, Today.AddDays(1));

            // Assert
            Assert.That(result.Length, Is.EqualTo(3));

            var minValues = result[0];
            Assert.That(minValues[0].Timestamp, Is.EqualTo(TodayMs));
            Assert.That(minValues[0].Value, Is.EqualTo(0));
            Assert.That(minValues[1].Timestamp, Is.EqualTo(TodayMs + 1 * 3600 * 1000));
            Assert.That(minValues[1].Value, Is.EqualTo(25));
            Assert.That(minValues[2].Timestamp, Is.EqualTo(TodayMs + 2 * 3600 * 1000));
            Assert.That(minValues[2].Value, Is.EqualTo(25));

            var maxValues = result[1];
            Assert.That(maxValues[0].Timestamp, Is.EqualTo(TodayMs));
            Assert.That(maxValues[0].Value, Is.EqualTo(0));
            Assert.That(maxValues[1].Timestamp, Is.EqualTo(TodayMs + 1 * 3600 * 1000));
            Assert.That(maxValues[1].Value, Is.EqualTo(27));
            Assert.That(maxValues[2].Timestamp, Is.EqualTo(TodayMs + 2 * 3600 * 1000));
            Assert.That(maxValues[2].Value, Is.EqualTo(29));

            var avgValues = result[2];
            Assert.That(avgValues[0].Timestamp, Is.EqualTo(TodayMs));
            Assert.That(avgValues[0].Value, Is.EqualTo(0));
            Assert.That(avgValues[1].Timestamp, Is.EqualTo(TodayMs + 1 * 3600 * 1000));
            Assert.That(avgValues[1].Value, Is.EqualTo(26).Within(0.0001));
            Assert.That(avgValues[2].Timestamp, Is.EqualTo(TodayMs + 2 * 3600 * 1000));
            Assert.That(avgValues[2].Value, Is.EqualTo(27).Within(0.0001));
        }

        private static StatisticsDbEntry NewMeterStatisticsDbEntry(DateTime start, DateTime end, double value)
        {
            return new StatisticsDbEntry {Domain = Domain, Address = Address, Parameter = MeterParameter, TimeStart = start, TimeEnd = end, AvgValue = value};
        }

        private static StatisticsDbEntry NewMeterStatisticsDbEntryM2(DateTime start, DateTime end, double value)
        {
            return new StatisticsDbEntry {Domain = Domain, Address = $"{Address}_2", Parameter = MeterParameter, TimeStart = start, TimeEnd = end, AvgValue = value};
        }

        private static StatisticsDbEntry NewSensorStatisticsDbEntry(DateTime start, DateTime end, double value)
        {
            return new StatisticsDbEntry {Domain = Domain, Address = Address, Parameter = SensorParameter, TimeStart = start, TimeEnd = end, AvgValue = value};
        }

        private static StatisticsDbEntry NewSensorStatisticsDbEntryM2(DateTime start, DateTime end, double value)
        {
            return new StatisticsDbEntry {Domain = Domain, Address = $"{Address}_2", Parameter = SensorParameter, TimeStart = start, TimeEnd = end, AvgValue = value};
        }
    }
}
