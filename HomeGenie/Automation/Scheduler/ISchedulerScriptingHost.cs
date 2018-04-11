using System;
using HomeGenie.Automation.Scripting;
using HomeGenie.Data;
using HomeGenie.Service;

namespace HomeGenie.Automation.Scheduler
{
    public interface ISchedulerScriptingHost
    {
        void SetHost(HomeGenieService hg, SchedulerItem item);
        void RouteModuleEvent(ProgramManager.RoutedEvent eventData);
        SchedulerScriptingHost OnModuleUpdate(Action<ModuleHelper, ModuleParameter> handler);
        ProgramHelperBase Program { get; }
        ModulesManager Modules { get; }
        ModulesManager BoundModules { get; }
        SettingsHelper Settings { get; }
        NetHelper Net { get; }
        SerialPortHelper SerialPort { get; }
        TcpClientHelper TcpClient { get; }
        UdpClientHelper UdpClient { get; }
        MqttClientHelper MqttClient { get; }
        KnxClientHelper KnxClient { get; }
        SchedulerHelper Scheduler { get; }
        ModuleParameter Data(string name);
        void Pause(double seconds);
        void Delay(double seconds);
        void Say(string sentence, string locale = null, bool goAsync = false);
        void Reset();
    }
}