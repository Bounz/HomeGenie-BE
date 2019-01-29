using System;
using System.Collections.Generic;
using HomeGenie.Automation.Scheduler;
using NUnit.Framework;

namespace HomeGenie.UnitTests
{
    [TestFixture]
    public class SchedulerServiceTest
    {
        private SchedulerService _scheduler;
        private DateTime _start;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            _scheduler = new SchedulerService(null);
            _start = new DateTime(2017, 01, 01);
        }

        [Test]
        public void BasicCronExpression()
        {
            var expression = "0 * * * *";
            var occurrences = GetOccurrencesForDate(_scheduler, _start, expression);

            DisplayOccurrences(expression, occurrences);
            Assert.That(occurrences.Count, Is.EqualTo(24));
        }

        [Test]
        [TestCase("(30 22 * * *) > (49 22 * * *)", 20)]
        [TestCase("(50 23 * * *) > (9 0 * * *)", 20)]
        [TestCase("(50 23 * * *) > (9 1 * * *)", 80)]
        public void CronExpressionWithSpan(string expression, int expectedOccurrences)
        {
            var occurrences = GetOccurrencesForDate(_scheduler, _start, expression);

            DisplayOccurrences(expression, occurrences);
            Assert.That(occurrences.Count, Is.EqualTo(expectedOccurrences));
        }

        [Test]
        [TestCase("(30 * * * *) ; (* 22,23 * * *)")]
        [TestCase("(30 * * * *) & (* 22,23 * * *)")]
        public void CronExpressionWithAnd(string expression)
        {
            var occurrences = GetOccurrencesForDate(_scheduler, _start, expression);

            DisplayOccurrences(expression, occurrences);
            Assert.That(occurrences.Count, Is.EqualTo(2));
        }

        [Test]
        [TestCase("(30 22 * * *) : (49 22 * * *)")]
        [TestCase("(30 22 * * *) | (49 22 * * *)")]
        public void CronExpressionWithOr(string expression)
        {
            var occurrences = GetOccurrencesForDate(_scheduler, _start, expression);

            DisplayOccurrences(expression, occurrences);
            Assert.That(occurrences.Count, Is.EqualTo(2));
        }

        [Test]
        [TestCase("(30 * * * *) % (* 1-12 * * *)")]
        [TestCase("(30 * * * *) ! (* 1-12 * * *)")]
        public void CronExpressionWithExcept(string expression)
        {
            var occurrences = GetOccurrencesForDate(_scheduler, _start, expression);

            DisplayOccurrences(expression, occurrences);
            Assert.That(occurrences.Count, Is.EqualTo(12));
        }

        [Test]
        public void Test()
        {
            var expression = "0 0 1 1 *";
            var occurrences = GetOccurrencesForYear(_scheduler, _start, expression);

            DisplayOccurrences(expression, occurrences);
            //Assert.That(occurrences.Count, Is.EqualTo(12));
            //59 59 23 ? 11 THU#4 *
        }

        private static List<DateTime> GetOccurrencesForDate(SchedulerService scheduler, DateTime date, string expression)
        {
            date = DateTime.SpecifyKind(date, DateTimeKind.Local);
            return scheduler.GetScheduling(date.Date, date.Date.AddHours(24).AddMinutes(-1), expression);
        }

        private void DisplayOccurrences(string cronExpression, List<DateTime> occurrences)
        {
            Console.WriteLine("Cron expression: {0}", cronExpression);
            foreach (var dateTime in occurrences)
            {
                Console.WriteLine(dateTime.ToString("yyyy.MM.dd HH:mm:ss"));
            }
        }

        private static List<DateTime> GetOccurrencesForYear(SchedulerService scheduler, DateTime date, string expression)
        {
            date = DateTime.SpecifyKind(date, DateTimeKind.Local);
            var start = new DateTime(date.Year, 1, 1, 0, 0, 0 , DateTimeKind.Local);
            var end = new DateTime(date.Year, 12, 31, 23, 59, 59 , DateTimeKind.Local);
            return scheduler.GetScheduling(start, end, expression);
        }
    }
}
