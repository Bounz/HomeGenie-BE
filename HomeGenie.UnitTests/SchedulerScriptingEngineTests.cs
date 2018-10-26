using System;
using HomeGenie.Automation.Scheduler;
using HomeGenie.Service;
using Jint;
using Moq;
using NUnit.Framework;

namespace HomeGenieTests
{
    [TestFixture]
    public class SchedulerScriptingEngineTests
    {
        [Test]
        public void Program_WihoutUseOfScriptingHost_Works()
        {
            var jintEngine = new Engine();
            jintEngine.SetValue("hg", new Object());
            jintEngine.Execute(SchedulerScriptingEngine.InitScript);
        }

        [Test]
        public void Program_Say_Works()
        {
            var param = "";
            var scriptingHost = new Mock<ISchedulerScriptingHost>();
            scriptingHost
                .Setup(x => x.Say(It.IsAny<string>(), null, false))
                .Callback((string x, string s, bool b) => param = x);

            var jintEngine = new Engine();
            jintEngine.SetValue("hg", scriptingHost.Object);

            jintEngine.Execute(SchedulerScriptingEngine.InitScript + "hg.Say('test', null, false);");

            Assert.That(param, Is.EqualTo("test"));
        }
    }
}
