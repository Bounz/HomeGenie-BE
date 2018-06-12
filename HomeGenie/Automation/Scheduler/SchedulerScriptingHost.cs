using System;
using HomeGenie.Service;

using HomeGenie.Automation.Scripting;
using HomeGenie.Data;
using System.Threading;
using HomeGenie.Service.Constants;

namespace HomeGenie.Automation.Scheduler
{

    public class MethodRunResult
    {
        public Exception Exception = null;
        public object ReturnValue = null;
    }

    [Serializable]
    public class SchedulerScriptingHost : ISchedulerScriptingHost
    {

        private HomeGenieService homegenie = null;
        private SchedulerItem schedulerItem = null;
        private Store localStore;
        //
        private NetHelper netHelper;
        private SerialPortHelper serialPortHelper;
        private TcpClientHelper tcpClientHelper;
        private UdpClientHelper udpClientHelper;
        private MqttClientHelper mqttClientHelper;
        private KnxClientHelper knxClientHelper;
        private SchedulerHelper schedulerHelper;
        private ProgramHelperBase programHelper;
        private StoreHelper storeHelper;
        private Action<ModuleHelper, ModuleParameter> moduleUpdateHandler;

        public SchedulerScriptingHost()
        {
            localStore = new Store();
            storeHelper = new StoreHelper(new TsList<Store>(){localStore}, "local");
        }

        public void SetHost(HomeGenieService hg, SchedulerItem item)
        {
            homegenie = hg;
            schedulerItem = item;
            Reset();
            netHelper = new NetHelper(homegenie.Parameters, homegenie.GetHttpServicePort());
            serialPortHelper = new SerialPortHelper();
            tcpClientHelper = new TcpClientHelper();
            udpClientHelper = new UdpClientHelper();
            mqttClientHelper = new MqttClientHelper();
            knxClientHelper = new KnxClientHelper();
            schedulerHelper = new SchedulerHelper(homegenie);
            programHelper = new ProgramHelperBase(homegenie);
        }

        public void RouteModuleEvent(ProgramManager.RoutedEvent eventData)
        {
            if (moduleUpdateHandler != null)
            {
                var module = new ModuleHelper(homegenie, eventData.Module);
                var parameter = eventData.Parameter;
                var callback = new WaitCallback((state) =>
                {
                    try
                    {
                        homegenie.MigService.RaiseEvent(
                            this,
                            Domains.HomeAutomation_HomeGenie,
                            SourceModule.Scheduler,
                            "Scheduler Routed Event",
                            Properties.SchedulerModuleUpdateStart,
                            schedulerItem.Name);
                        moduleUpdateHandler(module, parameter);
                        homegenie.MigService.RaiseEvent(
                            this,
                            Domains.HomeAutomation_HomeGenie,
                            SourceModule.Scheduler,
                            "Scheduler Routed Event",
                            Properties.SchedulerModuleUpdateEnd,
                            schedulerItem.Name);
                    }
                    catch (Exception e) 
                    {
                        homegenie.MigService.RaiseEvent(
                            this,
                            Domains.HomeAutomation_HomeGenie,
                            SourceModule.Scheduler,
                            e.Message.Replace('\n', ' ').Replace('\r', ' '),
                            Properties.SchedulerError,
                            schedulerItem.Name);
                    }
                });
                ThreadPool.QueueUserWorkItem(callback);
            }
        }

        public SchedulerScriptingHost OnModuleUpdate(Action<ModuleHelper, ModuleParameter> handler)
        {
            moduleUpdateHandler = handler;
            return this;
        }

        public ProgramHelperBase Program => programHelper;

        public ModulesManager Modules => new ModulesManager(homegenie);

        public ModulesManager BoundModules
        {
            get
            {
                var boundModulesManager = new ModulesManager(homegenie);
                boundModulesManager.ModulesListCallback = sender => {
                    var modules = new TsList<Module>();
                    foreach(var m in schedulerItem.BoundModules) {
                        var mod = homegenie.Modules.Find(e=>e.Address == m.Address && e.Domain == m.Domain);
                        if (mod != null)
                            modules.Add(mod);
                    }
                    return modules;
                };
                return boundModulesManager;
            }
        }

        public SettingsHelper Settings => new SettingsHelper(homegenie);

        public NetHelper Net => netHelper;

        public SerialPortHelper SerialPort => serialPortHelper;

        public TcpClientHelper TcpClient => tcpClientHelper;

        public UdpClientHelper UdpClient => udpClientHelper;

        public MqttClientHelper MqttClient => mqttClientHelper;

        public KnxClientHelper KnxClient => knxClientHelper;

        public SchedulerHelper Scheduler => schedulerHelper;

        public ModuleParameter Data(string name)
        {
            return storeHelper.Get(name);
        }

        public void Pause(double seconds)
        {
            Thread.Sleep((int)(seconds * 1000));
        }

        public void Delay(double seconds)
        {
            Pause(seconds);
        }

        public void Say(string sentence, string locale = null, bool goAsync = false)
        {
            if (string.IsNullOrWhiteSpace(locale))
            {
                locale = Thread.CurrentThread.CurrentCulture.Name;
            }
            try
            {
                SpeechUtils.Say(sentence, locale, goAsync);
            }
            catch (Exception e) 
            {
                HomeGenieService.LogError(e);
            }
        }

        public void Reset()
        {
            try { serialPortHelper.Reset(); } catch { }
            try { tcpClientHelper.Reset(); } catch { }
            try { udpClientHelper.Reset(); } catch { }
            try { netHelper.Reset(); } catch { }
            try { mqttClientHelper.Reset(); } catch { }
            try { knxClientHelper.Reset(); } catch { }
        }

    }

}
