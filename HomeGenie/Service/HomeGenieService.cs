using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml.Serialization;
using System.Threading;
using System.Xml;
using HomeGenie.Automation;
using HomeGenie.Data;
using HomeGenie.Service.Constants;
using HomeGenie.Service.Logging;
using HomeGenie.Automation.Scheduler;
using HomeGenie.Database;
using HomeGenie.Service.Packages;
using HomeGenie.Service.Updates;
using HomeGenie.Utils;
using MIG;
using MIG.Gateways;
using NLog;
using OpenSource.UPnP;

namespace HomeGenie.Service
{
    [Serializable]
    public class HomeGenieService
    {
        #region Private Fields declaration

        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private MigService _migService;
        private WebServiceGateway _webGateway;
        private ProgramManager _masterControlProgram;
        private VirtualMeter _virtualMeter;
        //private UpdateChecker updateChecker;
        private UpdateManager _updateManager;
        private BackupManager _backupManager;
        private PackageManager _packageManager;
        private StatisticsCoreService _statisticsLogger;
        // Internal data structures
        private TsList<Module> _systemModules = new TsList<Module>();
        private TsList<Module> _modulesGarbage = new TsList<Module>();
        private TsList<VirtualModule> _virtualModules = new TsList<VirtualModule>();
        private List<Group> _automationGroups = new List<Group>();
        private List<Group> _controlGroups = new List<Group>();

        private SystemConfiguration _systemConfiguration;

        // public events
        //public event Action<LogEntry> LogEventAction;

        #endregion

        #region Web Service Handlers declaration

        private Handlers.Config _wshConfig;
        private Handlers.Automation _wshAutomation;
        private Handlers.Interconnection _wshInterconnection;
        private Handlers.Statistics _wshStatistics;

        #endregion

        #region Lifecycle

        public HomeGenieService()
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            EnsureDirectoryStructure();
            FinalizeRestoreProcess();
            EnableOutputRedirect();

            InitializeSystem();
            Reload();

            _backupManager = new BackupManager(this);
            _packageManager = new PackageManager(this);

            _statisticsLogger = new StatisticsCoreService(this, new StatisticsRepository(), new RealDateTime());
            _statisticsLogger.Start();

            // Setup local UPnP device
            SetupUpnp();

            ConfigureUpdater();

            Thread.Sleep(5000); // TODO: why do we need this?

            Start();
        }

        private static void EnsureDirectoryStructure()
        {
            if (!Directory.Exists(FilePaths.DataFolder))
                Directory.CreateDirectory(FilePaths.DataFolder);

            if(!File.Exists(FilePaths.SystemConfigFilePath))
                File.Copy(FilePaths.DefaultSystemConfigFilePath, FilePaths.SystemConfigFilePath);

            if(!File.Exists(FilePaths.AutomationProgramsFilePath))
                File.Copy(FilePaths.DefaultAutomationGroupsConfigFilePath, FilePaths.AutomationProgramsFilePath);

            if(!File.Exists(FilePaths.GroupsFilePath))
                File.Copy(FilePaths.DefaultGroupsConfigFilePath, FilePaths.GroupsFilePath);

            if(!File.Exists(FilePaths.InstalledPackagesFilePath))
                File.Copy(FilePaths.DefaultInstalledPackagesConfigFilePath, FilePaths.InstalledPackagesFilePath);

            if(!File.Exists(FilePaths.ModulesFilePath))
                File.Copy(FilePaths.DefaultModulesConfigFilePath, FilePaths.ModulesFilePath);

            if(!File.Exists(FilePaths.ProgramsFilePath))
                File.Copy(FilePaths.DefaultProgramsConfigFilePath, FilePaths.ProgramsFilePath);

            if(!File.Exists(FilePaths.SchedulerFilePath))
                File.Copy(FilePaths.DefaultSchedulerConfigFilePath, FilePaths.SchedulerFilePath);

            if (!Directory.Exists(FilePaths.ProgramsFolder))
            {
                Directory.CreateDirectory(FilePaths.ProgramsFolder);
                Utility.CopyFilesRecursively(new DirectoryInfo(FilePaths.DefaultProgramsFolder), new DirectoryInfo(FilePaths.ProgramsFolder));
            }

            if (!Directory.Exists(FilePaths.InterfacesFolder))
            {
                Directory.CreateDirectory(FilePaths.InterfacesFolder);
                Utility.CopyFilesRecursively(new DirectoryInfo(FilePaths.DefaultInterfacesFolder), new DirectoryInfo(FilePaths.InterfacesFolder));
            }

            if (!Directory.Exists(FilePaths.InterfacesFolder))
                Directory.CreateDirectory(FilePaths.InterfacesFolder);

            if (!Directory.Exists(FilePaths.WidgetsFolder))
                Directory.CreateDirectory(FilePaths.WidgetsFolder);
        }

        private void FinalizeRestoreProcess()
        {
            if(!Directory.Exists(BackupManager.RestoreTempFolder))
                return;

            var success = true;

            try
            {
                Utility.CopyFilesRecursively(new DirectoryInfo(BackupManager.RestoreTempFolder), new DirectoryInfo(FilePaths.DataFolder), true);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                success = false;
            }

            Directory.Delete(BackupManager.RestoreTempFolder, true);
            Console.WriteLine($"= Status: Backup Restore {(success ? "Succesful" : "Errors")}");
        }

        private void ConfigureUpdater()
        {
            _updateManager = new UpdateManager(this);

            var updateChecker = _updateManager.UpdateChecker;
            var updateInstaller = _updateManager.UpdateInstaller;

            updateChecker.UpdateProgress += (sender, args) =>
            {
                RaiseEvent(
                    Domains.HomeGenie_System,
                    Domains.HomeGenie_UpdateChecker,
                    SourceModule.Master,
                    "HomeGenie Update Checker",
                    Properties.InstallProgressUpdate,
                    args.Status.ToString()
                );
            };

            updateInstaller.ArchiveDownloadUpdate += (sender, args) =>
            {
                RaiseEvent(
                    Domains.HomeGenie_System,
                    Domains.HomeGenie_UpdateChecker,
                    SourceModule.Master,
                    "HomeGenie Update Checker",
                    Properties.InstallProgressMessage,
                    "= " + args.Status + ": " + args.ReleaseInfo.DownloadUrl
                );
            };

            updateInstaller.InstallProgressMessage += (sender, message) =>
            {
                RaiseEvent(
                    Domains.HomeGenie_System,
                    Domains.HomeGenie_UpdateChecker,
                    SourceModule.Master,
                    "HomeGenie Update Checker",
                    Properties.InstallProgressMessage,
                    message
                );
            };

            _updateManager.Start();
        }

        private void Start()
        {
            RaiseEvent(Domains.HomeGenie_System, Domains.HomeGenie_System, SourceModule.Master, "HomeGenie System", Properties.HomeGenieStatus, "STARTED");
            // Signal "SystemStarted" event to automation programs
            for (int p = 0; p < _masterControlProgram.Programs.Count; p++)
            {
                try
                {
                    var pb = _masterControlProgram.Programs[p];
                    if (pb.IsEnabled)
                    {
                        if (pb.Engine.SystemStarted != null)
                        {
                            if (!pb.Engine.SystemStarted())
                            // stop routing this event to other listeners
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    LogError(e);
                }
            }
        }

        public void Stop(bool saveData = true)
        {
            RaiseEvent(Domains.HomeGenie_System, Domains.HomeGenie_System, SourceModule.Master, "HomeGenie System", Properties.HomeGenieStatus, "STOPPING");
            // Signal "SystemStopping" event to automation programs
            for (int p = 0; p < _masterControlProgram.Programs.Count; p++)
            {
                try
                {
                    var pb = _masterControlProgram.Programs[p];
                    if (pb.IsEnabled)
                    {
                        if (pb.Engine.SystemStopping != null && !pb.Engine.SystemStopping())
                        {
                            // stop routing this event to other listeners
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    LogError(e);
                }
            }

            // Save system data before quitting
            if (saveData)
                SaveData();

            // Stop HG helper services
            _updateManager?.Stop();
            _statisticsLogger?.Stop();

            RaiseEvent(Domains.HomeGenie_System, Domains.HomeGenie_System, SourceModule.Master, "HomeGenie System", Properties.HomeGenieStatus, "VirtualMeter STOPPING");
            if (_virtualMeter != null) _virtualMeter.Stop();
            RaiseEvent(Domains.HomeGenie_System, Domains.HomeGenie_System, SourceModule.Master, "HomeGenie System", Properties.HomeGenieStatus, "VirtualMeter STOPPED");
            RaiseEvent(Domains.HomeGenie_System, Domains.HomeGenie_System, SourceModule.Master, "HomeGenie System", Properties.HomeGenieStatus, "MIG Service STOPPING");
            if (_migService != null) _migService.StopService();
            RaiseEvent(Domains.HomeGenie_System, Domains.HomeGenie_System, SourceModule.Master, "HomeGenie System", Properties.HomeGenieStatus, "MIG Service STOPPED");
            RaiseEvent(Domains.HomeGenie_System, Domains.HomeGenie_System, SourceModule.Master, "HomeGenie System", Properties.HomeGenieStatus, "ProgramEngine STOPPING");
            if (_masterControlProgram != null)
                _masterControlProgram.Enabled = false;
            RaiseEvent(Domains.HomeGenie_System, Domains.HomeGenie_System, SourceModule.Master, "HomeGenie System", Properties.HomeGenieStatus, "ProgramEngine STOPPED");
            RaiseEvent(Domains.HomeGenie_System, Domains.HomeGenie_System, SourceModule.Master, "HomeGenie System", Properties.HomeGenieStatus, "STOPPED");

            SystemLogger.Instance.Dispose();
        }

        public void SaveData()
        {
            RaiseEvent(Domains.HomeGenie_System, Domains.HomeGenie_System, SourceModule.Master, "HomeGenie System", Properties.HomeGenieStatus, "SAVING DATA");
            _systemConfiguration?.Update();
            UpdateModulesDatabase();
            UpdateSchedulerDatabase();
        }

        ~HomeGenieService()
        {
            Stop();
        }

        #endregion

        #region Data Wrappers - Public Members

        // Control groups (i.e. rooms, Outside, Housewide)
        public List<Group> Groups => _controlGroups;

        // Automation groups
        public List<Group> AutomationGroups => _automationGroups;

        // MIG interfaces
        public List<MigInterface> Interfaces => _migService.Interfaces;

        // Modules
        public TsList<Module> Modules => _systemModules;

        // Virtual modules
        public TsList<VirtualModule> VirtualModules => _virtualModules;

        // HomeGenie system parameters
        public List<ModuleParameter> Parameters => _systemConfiguration.HomeGenie.Settings;

        // Reference to SystemConfiguration
        public SystemConfiguration SystemConfiguration => _systemConfiguration;

        // Reference to MigService
        public MigService MigService => _migService;

        // Reference to ProgramEngine
        public ProgramManager ProgramManager => _masterControlProgram;

        // Reference to UpdateChecked
        public UpdateChecker UpdateChecker => _updateManager.UpdateChecker;
        public UpdateInstaller UpdateInstaller => _updateManager.UpdateInstaller;

        // Reference to BackupManager
        public BackupManager BackupManager => _backupManager;

        // Reference to PackageManager
        public PackageManager PackageManager => _packageManager;

        // Reference to Statistics
        public StatisticsCoreService Statistics => _statisticsLogger;

        // Public utility methods
        public string GetHttpServicePort()
        {
            return _webGateway.GetOption("Port").Value;
        }

        public object InterfaceControl(MigInterfaceCommand cmd)
        {
            object response = null;
            var target = _systemModules.Find(m => m.Domain == cmd.Domain && m.Address == cmd.Address);
            bool isRemoteModule = (target != null && !String.IsNullOrWhiteSpace(target.RoutingNode));
            if (isRemoteModule)
            {
                try
                {
                    var domain = cmd.Domain;
                    if (domain.StartsWith("HGIC:"))
                        domain = domain.Substring(domain.IndexOf(".") + 1);
                    var serviceUrl = "http://" + target.RoutingNode + "/api/" + domain + "/" + cmd.Address + "/" + cmd.Command + "/" + cmd.OptionsString;
                    var netHelper = new Automation.Scripting.NetHelper(Parameters, GetHttpServicePort()).WebService(serviceUrl);
                    var username = _webGateway.GetOption("Username").Value;
                    var password = _webGateway.GetOption("Password").Value;
                    if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                    {
                        netHelper.WithCredentials(username, password);
                    }
                    response = netHelper.GetData();
                }
                catch (Exception ex)
                {
                    LogError(Domains.HomeAutomation_HomeGenie, "Interconnection:" + target.RoutingNode, ex.Message, "Exception.StackTrace", ex.StackTrace);
                }
            }
            else
            {
                var migInterface = _migService.GetInterface(cmd.Domain);
                if (migInterface != null)
                {
                    try
                    {
                        response = migInterface.InterfaceControl(cmd);
                    }
                    catch (Exception ex)
                    {
                        LogError(Domains.HomeAutomation_HomeGenie, "InterfaceControl", ex.Message, "Exception.StackTrace", ex.StackTrace);
                    }
                }
                //
                // Route command to Automation Programs' Dynamic API
                var r = ProgramDynamicApi.TryApiCall(cmd);
                if (r != null && !String.IsNullOrWhiteSpace(r.ToString()))
                {
                    // Automation Programs can eventually override MIG response
                    response = r;
                }
                //
                // Macro Recording
                //
                // TODO: find a better solution for this.... 
                // TODO: it was: migService_ServiceRequestPostProcess(this, new ProcessRequestEventArgs(cmd));
                // TODO: !IMPORTANT!
                if (_masterControlProgram != null && _masterControlProgram.MacroRecorder.IsRecordingEnabled && cmd != null && cmd.Command != null && (cmd.Command.StartsWith("Control.") || (cmd.Command.StartsWith("AvMedia.") && cmd.Command != "AvMedia.Browse" && cmd.Command != "AvMedia.GetUri")))
                {
                    _masterControlProgram.MacroRecorder.AddCommand(cmd);
                }
            }
            return response;
        }

        public List<Group> GetGroups(string namePrefix)
        {
            List<Group> group = null;
            if (namePrefix.ToLower() == "automation")
            {
                group = _automationGroups;
            }
            else
            {
                group = _controlGroups;
            }
            return group;
        }

        public string GetJsonSerializedModules(bool hideProperties)
        {
            string jsonModules = "";
            try
            {
                jsonModules = "[";
                for (int m = 0; m < _systemModules.Count; m++)// Module m in Modules)
                {
                    jsonModules += Utility.Module2Json(_systemModules[m], hideProperties) + ",\n";
                    //System.Threading.Thread.Sleep(1);
                }
                jsonModules = jsonModules.TrimEnd(',', '\n');
                jsonModules += "]";
                // old code for generate json, it was too much cpu time consuming on ARM
                //jsonmodules = JsonConvert.SerializeObject(Modules, Formatting.Indented);
            }
            catch (Exception ex)
            {
                LogError(
                    Domains.HomeAutomation_HomeGenie,
                    "GetJsonSerializedModules()",
                    ex.Message,
                    "Exception.StackTrace",
                    ex.StackTrace
                );
            }
            //
            return jsonModules;
        }

        // TODO: move this to a better location
        public bool ExecuteAutomationRequest(MigInterfaceCommand command)
        {
            string levelValue, commandValue;
            // check for certain commands
            if (command.Command == Commands.Groups.GroupsLightsOff)
            {
                levelValue = "0";
                commandValue = Commands.Control.ControlOff;
            }
            else if (command.Command == Commands.Groups.GroupsLightsOn)
            {
                levelValue = "1";
                commandValue = Commands.Control.ControlOn;
            }
            else
            {
                return false;
            }
            //loop, turning off lights
            try
            {
                var group = Groups.Find(z => z.Name == command.GetOption(0));
                for (int m = 0; m < group.Modules.Count; m++)
                {
                    var module = Modules.Find(mod => mod.Domain == group.Modules[m].Domain && mod.Address == group.Modules[m].Address);
                    if (module != null && (module.DeviceType == ModuleTypes.Light || module.DeviceType == ModuleTypes.Dimmer))
                    {
                        try
                        {
                            var icmd = new MigInterfaceCommand(module.Domain + "/" + module.Address + "/" + commandValue);
                            InterfaceControl(icmd);
                            Utility.ModuleParameterGet(module, Properties.StatusLevel).Value = levelValue;
                        }
                        catch (Exception e)
                        {
                            LogError(e);
                        }
                    }
                }
            }
            catch
            {
                // TODO: handle exception here
            }
            return true;
        }

        #endregion

        #region MIG Events Propagation / Logging

        internal void RaiseEvent(object sender, MigEvent evt)
        {
            _migService.RaiseEvent(sender, evt);
        }

        internal void RaiseEvent(
            object sender,
            string domain,
            string source,
            string description,
            string property,
            string value
        )
        {
            var evt = _migService.GetEvent(domain, source, description, property, value);
            _migService.RaiseEvent(sender, evt);
        }

        internal static void LogDebug(
            string domain,
            string source,
            string description,
            string property,
            string value)
        {
            var debugEvent = new MigEvent(domain, source, description, property, value);
            MigService.Log.Debug(debugEvent);
        }

        internal static void LogError(
            string domain,
            string source,
            string description,
            string property,
            string value)
        {
            var errorEvent = new MigEvent(domain, source, description, property, value);
            LogError(errorEvent);
        }

        internal static void LogError(object err)
        {
            MigService.Log.Error(err);
        }

        private void EnableOutputRedirect()
        {
            Console.OutputEncoding = Encoding.UTF8;
            var outputRedirect = new ConsoleRedirect();
            outputRedirect.ProcessOutput = (outputLine) => {
                if (SystemLogger.Instance.IsLogEnabled)
                    SystemLogger.Instance.WriteToLog(outputLine);
            };
            Console.SetOut(outputRedirect);
            Console.SetError(outputRedirect);
        }

        #endregion

        #region MIG Service events handling

        private void migService_InterfaceModulesChanged(object sender, InterfaceModulesChangedEventArgs args)
        {
            modules_RefreshInterface(_migService.GetInterface(args.Domain));
        }

        private void migService_InterfacePropertyChanged(object sender, InterfacePropertyChangedEventArgs args)
        {
            
            // look for module associated to this event
            Module module = Modules.Find(o => o.Domain == args.EventData.Domain && o.Address == args.EventData.Source);
            if (module != null && args.EventData.Property != "")
            {
                // clear RoutingNode property since the event was locally generated
                //if (module.RoutingNode != "")
                //{
                //    module.RoutingNode = "";
                //}
                // we found associated module in HomeGenie.Modules

                // Update/Add the module parameter as needed
                ModuleParameter parameter = null;
                try
                {
                    // Lookup for the existing module parameter
                    parameter = Utility.ModuleParameterGet(module, args.EventData.Property);
                    if (parameter == null)
                    {
                        parameter = new ModuleParameter() {
                            Name = args.EventData.Property,
                            Value = args.EventData.Value.ToString()
                        };
                        module.Properties.Add(parameter);
                        //parameter = Utility.ModuleParameterGet(module, args.EventData.Property);
                    }
                    else
                    {
                        parameter.Value = args.EventData.Value.ToString();
                    }
                }
                catch (Exception ex)
                {
                    LogError(ex);
                }
                // Prevent event pump from blocking on other worker tasks
                if (_masterControlProgram != null)
                Utility.RunAsyncTask(() =>
                {
                    _masterControlProgram.SignalPropertyChange(sender, module, args.EventData);
                });
            }
            else
            {
                if (args.EventData.Domain == Domains.MigService_Interfaces)
                {
                    modules_RefreshInterface(_migService.GetInterface(args.EventData.Source));
                }
                /*
                LogBroadcastEvent(
                    args.EventData.Domain,
                    args.EventData.Source,
                    args.EventData.Description,
                    args.EventData.Property,
                    args.EventData.Value != null ? args.EventData.Value.ToString() : ""
                );
                */
            }
        }

        private void migService_ServiceRequestPreProcess(object sender, ProcessRequestEventArgs args)
        {
            // Currently we only support requests coming from WebServiceGateway
            // TODO: in the future, add support for any MigGateway channel (eg. WebSocketGateway as well)
            if (args.Request.Context.Source != ContextSource.WebServiceGateway)
                return;

            var migCommand = args.Request.Command;

            #region Interconnection (Remote Node Command Routing)

            Module target = _systemModules.Find(m => m.Domain == migCommand.Domain && m.Address == migCommand.Address);
            bool isRemoteModule = (target != null && !String.IsNullOrWhiteSpace(target.RoutingNode));
            if (isRemoteModule)
            {
                try
                {
                    var domain = migCommand.Domain;
                    if (domain.StartsWith("HGIC:")) domain = domain.Substring(domain.IndexOf(".") + 1);
                    var serviceurl = "http://" + target.RoutingNode + "/api/" + domain + "/" + migCommand.Address + "/" + migCommand.Command + "/" + migCommand.OptionsString;
                    var neth = new Automation.Scripting.NetHelper(Parameters, GetHttpServicePort()).WebService(serviceurl);
                    var username = _webGateway.GetOption("Username").Value;
                    var password = _webGateway.GetOption("Password").Value;
                    if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                    {
                        neth.WithCredentials(
                            username,
                            password
                        );
                    }
                    neth.Call();
                }
                catch (Exception ex)
                {
                    LogError(
                        Domains.HomeAutomation_HomeGenie,
                        "Interconnection:" + target.RoutingNode,
                        ex.Message,
                        "Exception.StackTrace",
                        ex.StackTrace
                    );
                }
                return;
            }

            #endregion

            // HomeGenie Web Service domain API
            if (migCommand.Domain == Domains.HomeAutomation_HomeGenie)
            {
                // domain == HomeAutomation.HomeGenie
                switch (migCommand.Address)
                {

                case "Config":
                    _wshConfig.ProcessRequest(args.Request);
                    break;

                case "Automation":
                    _wshAutomation.ProcessRequest(args.Request);
                    break;

                case "Interconnection":
                    _wshInterconnection.ProcessRequest(args.Request);
                    break;

                case "Statistics":
                    _wshStatistics.ProcessRequest(args.Request);
                    break;

                }
            }
            else if (migCommand.Domain == Domains.HomeAutomation_HomeGenie_Automation)
            {
                int n;
                bool nodeIdIsNumeric = int.TryParse(migCommand.Address, out n);
                if (nodeIdIsNumeric)
                {
                    switch (migCommand.Command)
                    {

                    case "Control.Run":
                        _wshAutomation.ProgramRun(migCommand.Address, migCommand.GetOption(0));
                        break;

                    case "Control.Break":
                        _wshAutomation.ProgramBreak(migCommand.Address);
                        break;

                    }
                }
            }

        }

        private void migService_ServiceRequestPostProcess(object sender, ProcessRequestEventArgs args)
        {
            var command = args.Request.Command;
            if (command.Domain ==  Domains.MigService_Interfaces && command.Command.EndsWith(".Set"))
            {
                _systemConfiguration.Update();
            }

            // Let automation programs process the request; we append eventual POST data (RequestText) to the MigInterfaceCommand
            if (!String.IsNullOrWhiteSpace(args.Request.RequestText))
                command = new MigInterfaceCommand(command.OriginalRequest + "/" + args.Request.RequestText);
            args.Request.ResponseData = ProgramDynamicApi.TryApiCall(command);

            // Macro Recording
            if (_masterControlProgram != null && _masterControlProgram.MacroRecorder.IsRecordingEnabled && command != null && command.Command != null && (command.Command.StartsWith("Control.") || (command.Command.StartsWith("AvMedia.") && command.Command != "AvMedia.Browse" && command.Command != "AvMedia.GetUri")))
            {
                _masterControlProgram.MacroRecorder.AddCommand(command);
            }
        }

        #endregion

        #region Initialization and Data Persistence

        public bool UpdateAutomationGroupsDatabase()
        {
            return UpdateXmlDatabase(_automationGroups, FilePaths.AutomationProgramsFilePath);
        }

        public bool UpdateGroupsDatabase()
        {
            return UpdateXmlDatabase(_controlGroups, FilePaths.GroupsFilePath);
        }

        public bool UpdateModulesDatabase()
        {
            var success = false;
            modules_RefreshAll();
            lock (_systemModules.LockObject)
            {
                try
                {
                    // Due to encrypted values, we must clone modules before encrypting and saving
                    var clonedModules = _systemModules.DeepClone();
                    foreach (var module in clonedModules)
                    {
                        foreach (var parameter in module.Properties)
                        {
                            // these four properties have to be kept in clear text
                            if (parameter.Name != Properties.WidgetDisplayModule
                                && parameter.Name != Properties.VirtualModuleParentId
                                && parameter.Name != Properties.ProgramStatus
                                && parameter.Name != Properties.RuntimeError)
                            {
                                if (!string.IsNullOrEmpty(parameter.Value))
                                    parameter.Value = StringCipher.Encrypt(parameter.Value, GetPassPhrase());
                            }
                        }
                    }

                    success = UpdateXmlDatabase(clonedModules, FilePaths.ModulesFilePath);
                }
                catch (Exception ex)
                {
                    LogError(Domains.HomeAutomation_HomeGenie, "UpdateModulesDatabase()", ex.Message, "Exception.StackTrace", ex.StackTrace);
                }
            }
            return success;
        }

        public bool UpdateProgramsDatabase()
        {
            return UpdateXmlDatabase(_masterControlProgram.Programs, FilePaths.ProgramsFilePath);
        }

        public bool UpdateSchedulerDatabase()
        {
            return UpdateXmlDatabase(_masterControlProgram.SchedulerService.Items, FilePaths.SchedulerFilePath);
        }

        private static bool UpdateXmlDatabase<T>(T items, string filename)
        {
            var success = false;
            XmlWriter writer = null;
            try
            {
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
                if (File.Exists(filePath))
                    File.Delete(filePath);

                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    Encoding = Encoding.UTF8
                };
                var serializer = new XmlSerializer(typeof(T));
                writer = XmlWriter.Create(filePath, settings);
                serializer.Serialize(writer, items);
                writer.Flush();
                success = true;
            }
            catch (Exception e)
            {
                LogError(Domains.HomeAutomation_HomeGenie, $"UpdateXmlDatabase<{typeof(T).FullName}>()", e.Message, "StackTrace", e.StackTrace);
            }
            finally
            {
                writer?.Close();
            }
            return success;
        }

        /// <summary>
        /// Reload system configuration and restart services and interfaces.
        /// </summary>
        private void Reload()
        {
            _migService.StopService();
            LoadConfiguration();

            if (_webGateway == null)
            {
                RaiseEvent(
                    Domains.HomeGenie_System,
                    Domains.HomeAutomation_HomeGenie,
                    SourceModule.Master,
                    "Configuration entry not found",
                    "Gateways",
                    "WebServiceGateway"
                );
                Program.Quit(false);
            }
            var webPort = int.Parse(_webGateway.GetOption("Port").Value);

            var started = _migService.StartService();
            while (!started)
            {
                RaiseEvent(
                    Domains.HomeGenie_System,
                    Domains.HomeAutomation_HomeGenie,
                    SourceModule.Master,
                    "HTTP binding failed.",
                    Properties.SystemInfoHttpAddress,
                    _webGateway.GetOption("Host").Value + ":" + _webGateway.GetOption("Port").Value
                );
                // Try auto-binding to another port >= 8080 (up to 8090)
                if (webPort < 8080)
                    webPort = 8080;
                else
                    webPort++;
                if (webPort <= 8090)
                {
                    _webGateway.SetOption("Port", webPort.ToString());
                    started = _webGateway.Start();
                }
                else
                {
                    Program.Quit(false);
                }
            }

            RaiseEvent(
                Domains.HomeGenie_System,
                Domains.HomeAutomation_HomeGenie,
                SourceModule.Master,
                "HomeGenie service ready",
                Properties.SystemInfoHttpAddress,
                _webGateway.GetOption("Host").Value + ":" + _webGateway.GetOption("Port").Value
            );
        }

        /// <summary>
        /// Reload the system without stopping the web service.
        /// </summary>
        /// <returns><c>true</c> on success, <c>false</c> otherwise.</returns>
        public bool SoftReload()
        {
            bool success = true;
            foreach (var migInterface in _migService.Interfaces)
            {
                MigService.Log.Debug("Disabling Interface {0}", migInterface.GetDomain());
                migInterface.IsEnabled = false;
                migInterface.Disconnect();
            }
            LoadConfiguration();
            try
            {
                // Initialize MIG Interfaces
                foreach (MIG.Config.Interface iface in _migService.Configuration.Interfaces)
                {
                    if (iface.IsEnabled)
                    {
                        _migService.EnableInterface(iface.Domain);
                    }
                    else
                    {
                        _migService.DisableInterface(iface.Domain);
                    }
                }
            }
            catch (Exception e)
            {
                MigService.Log.Error(e);
                success = false;
            }
            return success;
        }

        public void LoadConfiguration()
        {
            LoadSystemConfig();
            _webGateway = (WebServiceGateway) _migService.GetGateway("WebServiceGateway");

            LoadModules();

            // load last saved groups data into controlGroups list
            try
            {
                var serializer = new XmlSerializer(typeof(List<Group>));
                using (var reader = new StreamReader(FilePaths.GroupsFilePath))
                    _controlGroups = (List<Group>)serializer.Deserialize(reader);
            }
            catch
            {
                //TODO: log error
            }

            // load last saved automation groups data into automationGroups list
            try
            {
                var serializer = new XmlSerializer(typeof(List<Group>));
                using (var reader = new StreamReader(FilePaths.AutomationProgramsFilePath))
                    _automationGroups = (List<Group>)serializer.Deserialize(reader);
            }
            catch
            {
                //TODO: log error
            }

            // load last saved programs data into masterControlProgram.Programs list
            if (_masterControlProgram != null)
            {
                _masterControlProgram.Enabled = false;
                _masterControlProgram = null;
            }
            _masterControlProgram = new ProgramManager(this);
            try
            {
                var serializer = new XmlSerializer(typeof(List<ProgramBlock>));
                using (var reader = new StreamReader(FilePaths.ProgramsFilePath))
                {
                    var programs = (List<ProgramBlock>)serializer.Deserialize(reader);
                    foreach (var program in programs)
                    {
                        program.IsRunning = false;
                        // backward compatibility with hg < 0.91
                        if (program.Address == 0)
                        {
                            // assign an id to program if unassigned
                            program.Address = _masterControlProgram.GeneratePid();
                        }
                        _masterControlProgram.ProgramAdd(program);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(
                    Domains.HomeAutomation_HomeGenie,
                    "LoadConfiguration()",
                    ex.Message,
                    "Exception.StackTrace",
                    ex.StackTrace
                );
            }

            // load last saved scheduler items data into masterControlProgram.SchedulerService.Items list
            try
            {
                var serializer = new XmlSerializer(typeof(List<SchedulerItem>));
                using (var reader = new StreamReader(FilePaths.SchedulerFilePath))
                {
                    var schedulerItems = (List<SchedulerItem>)serializer.Deserialize(reader);
                    _masterControlProgram.SchedulerService.Items.AddRange(schedulerItems);
                }
            }
            catch
            {
                //TODO: log error
            }

            // force re-generation of Modules list
            modules_RefreshAll();

            // enable automation programs engine
            _masterControlProgram.Enabled = true;
        }

        public void RestoreFactorySettings()
        {
            // Stop program engine
            try
            {
                _masterControlProgram.Enabled = false;
                _masterControlProgram = null;
            }
            catch
            {
            }

            // Uncompress factory settings and restart HG service
            ArchiveHelper.Unarchive("homegenie_factory_config.zip", AppDomain.CurrentDomain.BaseDirectory);
            Reload();
            SaveData();
        }

        #endregion Initialization and Data Storage

        #region Misc events handlers

        // fired after configuration is written to systemconfiguration.xml
        private void systemConfiguration_OnUpdate(bool success)
        {
            modules_RefreshAll();
        }
 
        #endregion

        #region Internals for modules' structure update and sorting

        internal void modules_RefreshVirtualModules()
        {
            lock (_systemModules.LockObject)
            lock (_virtualModules.LockObject)
            try
            {
                //
                // Virtual Modules
                //
                foreach (var virtualModule in _virtualModules)
                {
                    ProgramBlock program = _masterControlProgram.Programs.Find(p => p.Address.ToString() == virtualModule.ParentId);
                    if (program == null) continue;
                    //
                    var virtualModuleWidget = Utility.ModuleParameterGet(virtualModule, Properties.WidgetDisplayModule);
                    //
                    Module module = Modules.Find(o => {
                        // main program module...
                        bool found = (o.Domain == virtualModule.Domain && o.Address == virtualModule.Address && o.Address == virtualModule.ParentId);
                        // ...or virtual module
                        if (!found && o.Domain == virtualModule.Domain && o.Address == virtualModule.Address && o.Address != virtualModule.ParentId)
                        {
                            var prop = Utility.ModuleParameterGet(o, Properties.VirtualModuleParentId);
                            if (prop != null && prop.Value == virtualModule.ParentId) found = true;
                        }
                        return found;
                    });

                    if (!program.IsEnabled)
                    {
                        if (module != null && module.RoutingNode == "" && virtualModule.ParentId != module.Address)
                        {
                            // copy instance module properties to virtualmodules before removing
                            virtualModule.Name = module.Name;
                            virtualModule.DeviceType = module.DeviceType;
                            virtualModule.Properties.Clear();
                            foreach (var p in module.Properties)
                            {
                                virtualModule.Properties.Add(p);
                            }
                            _systemModules.Remove(module);
                        }
                        continue;
                    }
                    //else if (virtualModule.ParentId == virtualModule.Address)
                    //{
                    //    continue;
                    //}

                    if (module == null)
                    {
                        // add new module
                        module = new Module();
                        _systemModules.Add(module);
                        // copy properties from virtualmodules
                        foreach (var p in virtualModule.Properties)
                        {
                            module.Properties.Add(p);
                        }
                    }

                    // module inherits props from associated virtual module
                    module.Domain = virtualModule.Domain;
                    module.Address = virtualModule.Address;
                    if (module.DeviceType == ModuleTypes.Generic && virtualModule.DeviceType != ModuleTypes.Generic)
                    {
                        module.DeviceType = virtualModule.DeviceType;
                    }
                    // associated module's name of an automation program cannot be changed
                    if (module.Name == "" || (module.DeviceType == ModuleTypes.Program && virtualModule.Name != ""))
                    {
                        module.Name = virtualModule.Name;
                    }
                    module.Description = virtualModule.Description;
                    //
                    if (virtualModule.ParentId != virtualModule.Address)
                    {
                        Utility.ModuleParameterSet(
                            module,
                            Properties.VirtualModuleParentId,
                            virtualModule.ParentId
                        );
                    }
                    var moduleWidget = Utility.ModuleParameterGet(module, Properties.WidgetDisplayModule);
                    // if a widget is specified on virtual module then we force module to display using this
                    if ((virtualModuleWidget != null && (virtualModuleWidget.Value != "" || moduleWidget == null)) && (moduleWidget == null || (moduleWidget.Value != virtualModuleWidget.Value)))
                    {
                        Utility.ModuleParameterSet(
                            module,
                            Properties.WidgetDisplayModule,
                            virtualModuleWidget.Value
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(
                    Domains.HomeAutomation_HomeGenie,
                    "modules_RefreshVirtualModules()",
                    ex.Message,
                    "Exception.StackTrace",
                    ex.StackTrace
                );
            }
        }

        internal void modules_RefreshPrograms()
        {
            lock (_systemModules.LockObject)
            try
            {
                // Refresh ProgramEngine program modules
                if (_masterControlProgram != null)
                {
                    lock (_masterControlProgram.Programs.LockObject)
                    foreach (var program in _masterControlProgram.Programs)
                    {
                        Module module = _systemModules.Find(o => o.Domain == Domains.HomeAutomation_HomeGenie_Automation && o.Address == program.Address.ToString());
                        if (module != null && program.Type.ToLower() == "wizard" && !program.IsEnabled && module.RoutingNode == "")
                        {
                            // we don't remove non-wizard programs to keep configuration options
                            // TODO: ?? should use modulesGarbage in order to allow correct removing/restoring of all program types ??
                            // TODO: ?? (but it will loose config options when hg is restarted because modulesGarbage it's not saved) ??
                            _systemModules.Remove(module);
                            continue;
                        }
                        else if (module == null && !program.IsEnabled)
                        {
                            continue;
                        }
                        else if (module == null)
                        {
                            // add module for the program
                            module = new Module();
                            module.Domain = Domains.HomeAutomation_HomeGenie_Automation;
                            if (program.Type.ToLower() == "wizard")
                            {
                                Utility.ModuleParameterSet(
                                    module,
                                    Properties.WidgetDisplayModule,
                                    "homegenie/generic/program"
                                );
                            }
                            _systemModules.Add(module);
                        }
                        module.Name = program.Name;
                        module.Address = program.Address.ToString();
                        module.DeviceType = ModuleTypes.Program;
                        //module.Description = "Wizard Script";
                    }
                    // Add "Scheduler" virtual module
                    //Module scheduler = systemModules.Find(o=> o.Domain == Domains.HomeAutomation_HomeGenie && o.Address == SourceModule.Scheduler);
                    //if (scheduler == null) {
                    //    scheduler = new Module(){ Domain = Domains.HomeAutomation_HomeGenie, Address = SourceModule.Scheduler };
                    //    systemModules.Add(scheduler);
                    //}
                }
            }
            catch (Exception ex)
            {
                LogError(
                    Domains.HomeAutomation_HomeGenie,
                    "modules_RefreshPrograms()",
                    ex.Message,
                    "Exception.StackTrace",
                    ex.StackTrace
                );
            }
        }

        internal void modules_Sort()
        {
            lock (_systemModules.LockObject) try
            {
                // sort modules properties by name
                foreach (var module in _systemModules)
                {
                    module.Properties.Sort((ModuleParameter p1, ModuleParameter p2) =>
                    {
                        return p1.Name.CompareTo(p2.Name);
                    });
                }
                //
                // sort modules
                //
                _systemModules.Sort((Module m1, Module m2) =>
                {
                    System.Text.RegularExpressions.Regex re = new System.Text.RegularExpressions.Regex(@"([a-zA-Z]+)(\d+)");
                    System.Text.RegularExpressions.Match result1 = re.Match(m1.Address);
                    System.Text.RegularExpressions.Match result2 = re.Match(m2.Address);

                    string alphaPart1 = result1.Groups[1].Value.PadRight(8, '0');
                    string numberPart1 = (String.IsNullOrWhiteSpace(result1.Groups[2].Value) ? m1.Address.PadLeft(8, '0') : result1.Groups[2].Value.PadLeft(8, '0'));
                    string alphaPart2 = result2.Groups[1].Value.PadRight(8, '0');
                    string numberPart2 = (String.IsNullOrWhiteSpace(result2.Groups[2].Value) ? m2.Address.PadLeft(8, '0') : result2.Groups[2].Value.PadLeft(8, '0'));

                    string d1 = m1.Domain + "|" + alphaPart1 + numberPart1;
                    string d2 = m2.Domain + "|" + alphaPart2 + numberPart2;
                    return d1.CompareTo(d2);
                });

            }
            catch (Exception ex)
            {
                LogError(
                    Domains.HomeAutomation_HomeGenie,
                    "modules_Sort()",
                    ex.Message,
                    "Exception.StackTrace",
                    ex.StackTrace
                );
            }
        }

        internal void modules_RefreshAll()
        {
            _systemModules.RemoveAll(m => m == null); // <-- dunno why but sometimes it happen to have null entries causing exceptions

            // Refresh all MIG modules
            foreach (var iface in _migService.Interfaces)
            {
                try
                {
                    modules_RefreshInterface(iface);
                } catch {
                    //TODO: interface not ready? handle this
                }
            }

            // Refresh other HG modules
            modules_RefreshPrograms();
            modules_RefreshVirtualModules();

            modules_Sort();
        }

        private void modules_RefreshInterface(MigInterface iface)
        {
            if (_migService.Configuration.GetInterface(iface.GetDomain()).IsEnabled)
            {
                var interfaceModules = iface.GetModules();
                if (interfaceModules.Count > 0)
                {
                    // delete removed modules
                    var deleted = _systemModules.FindAll(m => m.Domain == iface.GetDomain() && (interfaceModules.Find(m1 => m1.Address == m.Address && m1.Domain == m.Domain) == null));
                    foreach (var mod in deleted)
                    {
                        // only "real" modules defined by mig interfaces are considered
                        var virtualParam = Utility.ModuleParameterGet(mod, Properties.VirtualModuleParentId);
                        if (virtualParam == null || virtualParam.DecimalValue == 0)
                        {
                            Module garbaged = _modulesGarbage.Find(m => m.Domain == mod.Domain && m.Address == mod.Address);
                            if (garbaged != null) _modulesGarbage.Remove(garbaged);
                            _modulesGarbage.Add(mod);
                            _systemModules.Remove(mod);
                        }
                    }
                    //
                    foreach (var migModule in interfaceModules)
                    {
                        Module module = _systemModules.Find(o => o.Domain == migModule.Domain && o.Address == migModule.Address);
                        if (module == null)
                        {
                            // try restoring from garbage
                            module = _modulesGarbage.Find(o => o.Domain == migModule.Domain && o.Address == migModule.Address);
                            if (module != null)
                            {
                                _systemModules.Add(module);
                            }
                            else
                            {
                                module = new Module();
                                module.Domain = migModule.Domain;
                                module.Address = migModule.Address;
                                _systemModules.Add(module);
                            }
                        }
                        if (String.IsNullOrEmpty(module.Description))
                        {
                            module.Description = migModule.Description;
                        }
                        if (module.DeviceType == ModuleTypes.Generic)
                        {
                            module.DeviceType = migModule.ModuleType;
                        }
                    }
                }
            }
            else
            {
                var deleted = _systemModules.FindAll(m => m.Domain == iface.GetDomain());
                foreach (var mod in deleted)
                {
                    var virtualParam = Utility.ModuleParameterGet(mod, Properties.VirtualModuleParentId);
                    if (virtualParam == null || virtualParam.DecimalValue == 0)
                    {
                        Module garbaged = _modulesGarbage.Find(m => m.Domain == mod.Domain && m.Address == mod.Address);
                        if (garbaged != null) _modulesGarbage.Remove(garbaged);
                        _modulesGarbage.Add(mod);
                        _systemModules.Remove(mod);
                    }
                }
            }
        }

        #endregion

        #region Private utility methods

        private void InitializeSystem()
        {
            // Setup web service handlers
            _wshConfig = new Handlers.Config(this);
            _wshAutomation = new Handlers.Automation(this);
            _wshInterconnection = new Handlers.Interconnection(this);
            _wshStatistics = new Handlers.Statistics(this);

            // Initialize MigService, gateways and interfaces
            _migService = new MigService();
            _migService.InterfaceModulesChanged += migService_InterfaceModulesChanged;
            _migService.InterfacePropertyChanged += migService_InterfacePropertyChanged;
            _migService.GatewayRequestPreProcess += migService_ServiceRequestPreProcess;
            _migService.GatewayRequestPostProcess += migService_ServiceRequestPostProcess;

            // Setup other objects used in HG
            _virtualMeter = new VirtualMeter(this);
        }

        private string GetPassPhrase()
        {
            // Get username/password from web serivce and use as encryption key
            var webGw = _migService.GetGateway("WebServiceGateway");
            if (webGw != null)
            {
                var username = webGw.GetOption("Username").Value;
                var password = webGw.GetOption("Password").Value;
                //return String.Format("{0}{1}homegenie", username, password);
                return $"{password}homegenie";
            }
            else
                return "";
        }

        private void LoadSystemConfig()
        {
            if (_systemConfiguration != null)
                _systemConfiguration.OnUpdate -= systemConfiguration_OnUpdate;
            try
            {
                var serializer = new XmlSerializer(typeof(SystemConfiguration));
                using (var reader = new StreamReader(FilePaths.SystemConfigFilePath))
                {
                    _systemConfiguration = (SystemConfiguration)serializer.Deserialize(reader);
                    // setup logging
                    if (!string.IsNullOrEmpty(_systemConfiguration.HomeGenie.EnableLogFile) && _systemConfiguration.HomeGenie.EnableLogFile.ToLower().Equals("true"))
                    {
                        SystemLogger.Instance.OpenLog();
                    }
                    else
                    {
                        SystemLogger.Instance.CloseLog();
                    }
                    // configure MIG
                    _migService.Configuration = _systemConfiguration.MigService;
                    // Set the password for decrypting settings values and later module parameters
                    _systemConfiguration.SetPassPhrase(GetPassPhrase());
                    // decrypt config data
                    foreach (var parameter in _systemConfiguration.HomeGenie.Settings)
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(parameter.Value))
                                parameter.Value = StringCipher.Decrypt(parameter.Value, GetPassPhrase());
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(
                    Domains.HomeAutomation_HomeGenie,
                    "LoadSystemConfig()",
                    ex.Message,
                    "Exception.StackTrace",
                    ex.StackTrace
                );
            }
            if (_systemConfiguration != null)
                _systemConfiguration.OnUpdate += systemConfiguration_OnUpdate;
        }

        private void LoadModules()
        {
            try
            {
                var serializer = new XmlSerializer(typeof(TsList<Module>));
                using (var reader = new StreamReader(FilePaths.ModulesFilePath))
                {
                    var modules = (TsList<Module>)serializer.Deserialize(reader);
                    foreach (var module in modules)
                    {
                        foreach (var parameter in module.Properties)
                        {
                            try
                            {
                                if (!String.IsNullOrEmpty(parameter.Value)) parameter.Value = StringCipher.Decrypt(
                                        parameter.Value,
                                        GetPassPhrase()
                                    );
                            }
                            catch
                            {
                            }
                        }
                    }
                    _modulesGarbage.Clear();
                    _systemModules.Clear();
                    _systemModules = modules;
                }
            }
            catch (Exception ex)
            {
                LogError(
                    Domains.HomeAutomation_HomeGenie,
                    "LoadModules()",
                    ex.Message,
                    "Exception.StackTrace",
                    ex.StackTrace
                );
            }
            try
            {
                // Reset Parameter.Watts, /*Status Level,*/ Sensor.Generic values
                for (int m = 0; m < _systemModules.Count; m++)
                {
                    // cleanup stuff for unwanted  xsi:nil="true" empty params
                    _systemModules[m].Properties.RemoveAll(p => p == null);
                    ModuleParameter parameter = _systemModules[m].Properties.Find(mp => mp.Name == Properties.MeterWatts /*|| mp.Name == Properties.STATUS_LEVEL || mp.Name == Properties.SENSOR_GENERIC */);
                    if (parameter != null)
                        parameter.Value = "0";
                }
            }
            catch (Exception ex)
            {
                LogError(
                    Domains.HomeAutomation_HomeGenie,
                    "LoadModules()",
                    ex.Message,
                    "Exception.StackTrace",
                    ex.StackTrace
                );
            }
            // Force re-generation of Modules list
            modules_RefreshAll();
        }

        private void SetupUpnp()
        {
            IPHostEntry host;
            string localIP = "";
            host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIP = ip.ToString();
                    break;
                }
            }
            string address = localIP;
            string bindhost = _webGateway.GetOption("Host").Value;
            string bindport = _webGateway.GetOption("Port").Value;
            if (bindhost.Length > 1)
            {
                address = bindhost;
            }
            //
            string presentationUrl = "http://" + address + ":" + bindport;
            //string friendlyName = "HomeGenie: " + Environment.MachineName;
            string manufacturer = "G-Labs";
            string manufacturerUrl = "http://bounz.github.io/HomeGenie-BE/";
            string modelName = "HomeGenie";
            string modelDescription = "HomeGenie Home Automation Server";
            //string modelURL = "http://homegenie.club/";
            string modelNumber = "HG-1";
            string standardDeviceType = "HomeAutomationServer";
            string uniqueDeviceName = _systemConfiguration.HomeGenie.GUID;
            if (String.IsNullOrEmpty(uniqueDeviceName))
            {
                _systemConfiguration.HomeGenie.GUID = uniqueDeviceName = Guid.NewGuid().ToString();
                _systemConfiguration.Update();
                // initialize database for first use
                //statisticsLogger.ResetDatabase(); //TODO do we really need to reset stats DB?
            }
            //
            var localDevice = UPnPDevice.CreateRootDevice(900, 1, "web\\");
            //hgdevice.Icon = null;
            if (presentationUrl != "")
            {
                localDevice.HasPresentation = true;
                localDevice.PresentationURL = presentationUrl;
            }
            localDevice.FriendlyName = modelName + ": " + Environment.MachineName;
            localDevice.Manufacturer = manufacturer;
            localDevice.ManufacturerURL = manufacturerUrl;
            localDevice.ModelName = modelName;
            localDevice.ModelDescription = modelDescription;
            if (Uri.IsWellFormedUriString(manufacturerUrl, UriKind.Absolute))
            {
                localDevice.ModelURL = new Uri(manufacturerUrl);
            }
            localDevice.ModelNumber = modelNumber;
            localDevice.StandardDeviceType = standardDeviceType;
            localDevice.UniqueDeviceName = uniqueDeviceName;
            localDevice.StartDevice();
        }

        #endregion
    }
}
