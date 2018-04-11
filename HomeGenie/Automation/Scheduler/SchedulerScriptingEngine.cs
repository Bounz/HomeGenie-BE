using System;
using System.Threading;
using HomeGenie.Service;
using HomeGenie.Service.Constants;

using Jint;

namespace HomeGenie.Automation.Scheduler
{
    public class SchedulerScriptingEngine
    {
        private HomeGenieService _hgService;
        private SchedulerItem _eventItem;
        private Thread _programThread;
        private bool _isRunning;

        private Engine _scriptEngine;
        private readonly SchedulerScriptingHost _hgScriptingHost;

        public const string InitScript = @"var $$ = {
          // ModulesManager
          modules: hg.Modules,
          boundModules: hg.BoundModules,

          // ProgramHelperBase
          program: hg.Program,

          // SettingsHelper
          settings: hg.Settings,

          // NetHelper
          net: hg.Net,

          // SerialPortHelper
          serial: hg.SerialPort,

          // TcpClientHelper
          tcp: hg.TcpClient,

          // UdpClientHelper
          udp: hg.UdpClient,

          // MqttClientHelper
          mqtt: hg.MqttClient,

          // KnxClientHelper
          knx: hg.KnxClient,

          // SchedulerHelper
          scheduler: hg.Scheduler,

          // Miscellaneous functions
          pause: function(seconds) { hg.Pause(seconds); },
          delay: function(seconds) { this.Pause(seconds); },
          event: hg.Event,
          say: hg.Say
        };
        $$.onNext = function() {
          var nextMin = new Date();
          nextMin.setSeconds(0);
          nextMin = new Date(nextMin.getTime()+60000);
          return $$.scheduler.IsOccurrence(nextMin, event.CronExpression);
        };
        $$.onPrevious = function() {
          var prevMin = new Date();
          prevMin.setSeconds(0);
          prevMin = new Date(prevMin.getTime()-60000);
          return $$.scheduler.IsOccurrence(prevMin, event.CronExpression);
        };
        $$.data = function(k,v) {
            if (typeof v == 'undefined') {
                return hg.Data(k).Value;
            } else {
                hg.Data(k).Value = v;
                return $$;
            }
        };
        $$.onUpdate = function(handler) {
            hg.OnModuleUpdate(handler);
        };
        ";

        public SchedulerScriptingEngine()
        {
            // we do not dispose the scripting host to keep volatile data persistent across instances
            _hgScriptingHost = new SchedulerScriptingHost();
        }

        public void SetHost(HomeGenieService hg, SchedulerItem item)
        {
            _hgService = hg;
            _eventItem = item;
            _scriptEngine = new Engine();
            _hgScriptingHost.SetHost(_hgService, item);
            _scriptEngine.SetValue("hg", _hgScriptingHost);
            _scriptEngine.SetValue("event", _eventItem);
        }

        public void Dispose()
        {
            StopScript();
        }

        public bool IsRunning => _isRunning;

        public void StartScript()
        {
            if (_hgService == null || _eventItem == null || _isRunning || string.IsNullOrWhiteSpace(_eventItem.Script))
                return;
            
            if (_programThread != null)
                StopScript();

            _isRunning = true;
            _hgService.RaiseEvent(this, Domains.HomeAutomation_HomeGenie, SourceModule.Scheduler, _eventItem.Name, Properties.SchedulerScriptStatus, _eventItem.Name+":Start");

            _programThread = new Thread(() =>
            {
                try
                {
                    MethodRunResult result = null;
                    try
                    {
                        _scriptEngine.Execute(InitScript+_eventItem.Script);
                    }
                    catch (Exception ex)
                    {
                        result = new MethodRunResult {Exception = ex};
                    }
                    _programThread = null;
                    _isRunning = false;
                    if (result != null && result.Exception != null && !result.Exception.GetType().Equals(typeof(System.Reflection.TargetException)))
                        _hgService.RaiseEvent(this, Domains.HomeAutomation_HomeGenie, SourceModule.Scheduler, _eventItem.Name, Properties.SchedulerScriptStatus, "Error ("+result.Exception.Message.Replace('\n', ' ').Replace('\r', ' ')+")");
                }
                catch (ThreadAbortException)
                {
                    _programThread = null;
                    _isRunning = false;
                    _hgService.RaiseEvent(this, Domains.HomeAutomation_HomeGenie, SourceModule.Scheduler, _eventItem.Name, Properties.SchedulerScriptStatus, "Interrupted");
                }
                _hgService.RaiseEvent(this, Domains.HomeAutomation_HomeGenie, SourceModule.Scheduler, _eventItem.Name, Properties.SchedulerScriptStatus, "End");
            });

            try
            {
                _programThread.Start();
            }
            catch
            {
                StopScript();
            }
        }

        public void StopScript()
        {
            _isRunning = false;
            if (_programThread != null)
            {
                try
                {
                    if (!_programThread.Join(1000))
                        _programThread.Abort();
                } catch { }
                _programThread = null;
            }
            if (_hgScriptingHost != null)
            {
                _hgScriptingHost.OnModuleUpdate(null);
                _hgScriptingHost.Reset();
            }
        }

        public void RouteModuleEvent(object eventData)
        {
            var moduleEvent = (ProgramManager.RoutedEvent)eventData;
            _hgScriptingHost.RouteModuleEvent(moduleEvent);
        }

    }
}

