using System.Collections.Generic;
using HomeGenie.Automation.Scripting;
using HomeGenie.Data;
using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;

namespace HomeGenie.UnitTests
{
    [TestFixture]
    public class SmtpHelperTests
    {
        [Test]
        public void SendMessage_MessageWithAttachments_ShouldLogMessageToSend()
        {
            // Arrange
            // We need to configure NLog, because right now in NetHelper class we use it directly.
            // After migration to .Net Core we will be able to use ILogger<T> interface for logger.
            var configuration = new LoggingConfiguration();
            var memoryTarget = new MemoryTarget {Name = "mem"};

            configuration.AddTarget(memoryTarget);
            configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, memoryTarget));
            LogManager.Configuration = configuration;

            var parameters = new List<ModuleParameter>
            {
                new ModuleParameter {Name = "Messaging.Email.SmtpServer", Value = "smtp.domain.tld"},
                new ModuleParameter {Name = "Messaging.Email.SmtpPort", Value = "25"}
            };

            var netHelper = new NetHelper(parameters, "");
            netHelper.AddAttachment("some.file", new byte[] {0x00});

            // Act
            netHelper.SendMessage("from@domain.tld", "to@domain.tld", "subject", "message text");

            // Assert
            var logs = memoryTarget.Logs;
            Assert.That(logs, Has.Some.Contains("going to send email"));
        }
    }
}
