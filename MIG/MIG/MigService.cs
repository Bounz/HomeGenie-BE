﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MIG.Config;
using NLog;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace MIG
{
    
    #region Event delegates

    /// <summary>
    /// Pre process request event handler.
    /// </summary>
    public delegate void PreProcessRequestEventHandler(object sender, ProcessRequestEventArgs args);
    /// <summary>
    /// Post process request event handler.
    /// </summary>
    public delegate void PostProcessRequestEventHandler(object sender, ProcessRequestEventArgs args);
    /// <summary>
    /// Interface property changed event handler.
    /// </summary>
    public delegate void InterfacePropertyChangedEventHandler(object sender, InterfacePropertyChangedEventArgs args);
    /// <summary>
    /// Interface modules changed event handler.
    /// </summary>
    public delegate void InterfaceModulesChangedEventHandler(object sender, InterfaceModulesChangedEventArgs args);
    /// <summary>
    /// Option changed event handler.
    /// </summary>
    public delegate void OptionChangedEventHandler(object sender, OptionChangedEventArgs args);

    #endregion

    public class MigService
    {
        private MigServiceConfiguration configuration;
        private DynamicApi dynamicApi;

        public static Logger Log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Occurs on gateway request pre process.
        /// </summary>
        public event PreProcessRequestEventHandler GatewayRequestPreProcess;
        /// <summary>
        /// Occurs on gateway request post process.
        /// </summary>
        public event PostProcessRequestEventHandler GatewayRequestPostProcess;
        /// <summary>
        /// Occurs when interface property changed.
        /// </summary>
        public event InterfacePropertyChangedEventHandler InterfacePropertyChanged;
        /// <summary>
        /// Occurs when interface modules changed.
        /// </summary>
        public event InterfaceModulesChangedEventHandler InterfaceModulesChanged;

        //TODO: add more events....
        //public event -- ServiceStarted;
        //public event -- ServiceStopped;

        public readonly List<IMigGateway> Gateways;
        public readonly List<MigInterface> Interfaces;

        #region Lifecycle

        public MigService()
        {
            Interfaces = new List<MigInterface>();
            Gateways = new List<IMigGateway>();
            configuration = new MigServiceConfiguration();
            dynamicApi = new DynamicApi();
        }

        private static void RedirectAssembly(string shortName) {
            ResolveEventHandler handler = null;

            handler = (sender, args) => {
                // Use latest strong name & version when trying to load SDK assemblies
                var requestedAssembly = new AssemblyName(args.Name);
                if (requestedAssembly.Name != shortName)
                    return null;

                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var assemblyToRedirectTo = assemblies.FirstOrDefault(x => x.GetName().Name == requestedAssembly.Name);
                if (assemblyToRedirectTo == null)
                    return null;

                Console.WriteLine("Redirecting assembly load of " + args.Name
                                                                  + ",\tloaded by " + (args.RequestingAssembly == null
                                                                      ? "(unknown)"
                                                                      : args.RequestingAssembly.FullName));

                AppDomain.CurrentDomain.AssemblyResolve -= handler;
                return assemblyToRedirectTo;
            };

            AppDomain.CurrentDomain.AssemblyResolve += handler;
        }

        /// <summary>
        /// Starts the service.
        /// </summary>
        /// <returns><c>true</c>, if service was started, <c>false</c> otherwise.</returns>
        public bool StartService()
        {
            bool success = true;
            try
            {
                // Start MIG Gateways
                foreach (var gw in Gateways)
                {
                    if (!gw.Start())
                    {
                        Log.Warn("Error starting Gateway {0}", gw.GetName());
                        success = false;
                    }
                }
                // Initialize MIG Interfaces
                foreach (Interface iface in configuration.Interfaces)
                {
                    if (iface.IsEnabled)
                    {
                        EnableInterface(iface.Domain);
                    }
                    else
                    {
                        DisableInterface(iface.Domain);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                success = false;
            }
            return success;
        }

        /// <summary>
        /// Stops the service.
        /// </summary>
        public void StopService()
        {
            Log.Debug("Stopping MigService");
            foreach (var migInterface in Interfaces)
            {
                Log.Debug("Disabling Interface {0}", migInterface.GetDomain());
                migInterface.IsEnabled = false;
                migInterface.Disconnect();
            }
            foreach (var gw in Gateways)
            {
                Log.Debug("Stopping Gateway {0}", gw.GetName());
                gw.Stop();
            }
            Log.Debug("Stopped MigService");
        }

        #endregion

        #region Public members

        /// <summary>
        /// Gets or sets the configuration.
        /// Behind the scenes creates gateways and interfaces.
        /// </summary>
        /// <value>The configuration.</value>
        public MigServiceConfiguration Configuration
        {
            get { return configuration; }
            set
            {
                // TODO: SHOULD DISPOSE PREVIOUS CONFIG AND ALLOCATED OBJECTS!!
                configuration = value;
                // Create MIG Gateways
                for (int g = 0; g < configuration.Gateways.Count; g++)
                {
                    var gwConfig = configuration.Gateways[g];
                    var gateway = AddGateway(gwConfig.Name);
//                    if (gateway != null && gwConfig.IsEnabled)
//                        gateway.Start();
                }
                // Create MIG interfaces
                for (int i = 0; i < configuration.Interfaces.Count; i++)
                {
                    var ifConfig = configuration.Interfaces[i];
                    var migInterface = AddInterface(ifConfig.Domain, ifConfig.AssemblyName);
//                    if (migInterface != null && ifConfig.IsEnabled)
//                        migInterface.Connect();
                }
            }
        }

        /// <summary>
        /// Gets the gateway.
        /// </summary>
        /// <returns>The gateway.</returns>
        /// <param name="className">Class name.</param>
        public IMigGateway GetGateway(string className)
        {
            return Gateways.Find(gw => gw.GetName().Equals(className));
        }

        /// <summary>
        /// Adds the gateway.
        /// </summary>
        /// <returns>The gateway.</returns>
        /// <param name="className">Class name.</param>
        /// <param name="assemblyName">Assembly name.</param>
        public IMigGateway AddGateway(string className, string assemblyName = "")
        {
            IMigGateway migGateway = GetGateway(className);
            if (migGateway == null)
            {
                try
                {
                    var type = TypeLookup("MIG.Gateways." + className, assemblyName);
                    if(type == null){
                        Log.Error("Can't find type for Mig Gateway MIG.Gateways.{0} (assemblyName={1})", className, assemblyName);
                        return null;
                    }
                    migGateway = (IMigGateway)Activator.CreateInstance(type);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
                if (migGateway != null)
                {
                    Log.Debug("Adding Gateway {0}", migGateway.GetName());
                    Gateways.Add(migGateway);
                    migGateway.PreProcessRequest += Gateway_PreProcessRequest;
                    migGateway.PostProcessRequest += Gateway_PostProcessRequest;
                }
            }
            // Try loading gateway settings from MIG configuration
            var config = configuration.GetGateway(migGateway.GetName());
            if (config == null)
            {
                config = new Gateway();
                config.Name = migGateway.GetName();
                if (config.Options == null)
                    config.Options = new List<Option>();
                configuration.Gateways.Add(config);
            }
            if (migGateway != null)
            {
                Log.Debug("Setting Gateway options");
                migGateway.Options = config.Options;
                foreach (var opt in configuration.GetGateway(migGateway.GetName()).Options)
                {
                    migGateway.SetOption(opt.Name, opt.Value);
                }
            }
            return migGateway;
        }

        /// <summary>
        /// Gets the interface.
        /// </summary>
        /// <returns>The interface.</returns>
        /// <param name="domain">Domain.</param>
        public MigInterface GetInterface(string domain)
        {
            return Interfaces.Find(iface => iface.GetDomain().Equals(domain));
        }

        /// <summary>
        /// Adds the interface.
        /// </summary>
        /// <returns>The interface.</returns>
        /// <param name="domain">Domain.</param>
        /// <param name="assemblyName">Assembly name.</param>
        public MigInterface AddInterface(string domain, string assemblyName = "")
        {
            MigInterface migInterface = GetInterface(domain);
            if (migInterface == null)
            {
                Type type = null;
                try
                {
                    type = TypeLookup("MIG.Interfaces." + domain, assemblyName);
                    if(type == null){
                        Log.Error("Can't find type for Mig Interface with domain {0} (assemblyName={1})", domain, assemblyName);
                        return null;
                    }
                    migInterface = (MigInterface)Activator.CreateInstance(type);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
                if (migInterface != null)
                {
                    var interfaceVersion = VersionLookup(type.Assembly);
                    Log.Debug("Adding Interface {0} Version: {1}", migInterface.GetDomain(), interfaceVersion);
                    Interfaces.Add(migInterface);
                    migInterface.InterfaceModulesChanged += MigService_InterfaceModulesChanged;
                    migInterface.InterfacePropertyChanged += MigService_InterfacePropertyChanged;
                }
            }

            // Try loading interface settings from MIG configuration
            var config = configuration.GetInterface(domain);
            if (config == null)
            {
                config = new Interface {Domain = domain};
                if (config.Options == null)
                    config.Options = new List<Option>();
                configuration.Interfaces.Add(config);
            }
            if (migInterface != null)
            {
                Log.Debug("Setting Interface options");
                migInterface.Options = config.Options;
                foreach (var opt in config.Options)
                {
                    migInterface.SetOption(opt.Name, opt.Value);
                }
            }
            return migInterface;
        }

        /// <summary>
        /// Removes the interface.
        /// </summary>
        /// <param name="domain">Domain.</param>
        public void RemoveInterface(string domain)
        {
            var migInterface = DisableInterface(domain);
            if (migInterface != null)
            {
                Log.Debug("Removing Interface {0}", domain);
                migInterface.InterfaceModulesChanged -= MigService_InterfaceModulesChanged;
                migInterface.InterfacePropertyChanged -= MigService_InterfacePropertyChanged;
                Interfaces.Remove(migInterface);
                configuration.GetInterface(domain).IsEnabled = false;
            }
            else
            {
                Log.Debug("Interface not found {0}", domain);
            }
        }

        /// <summary>
        /// Enables the interface.
        /// </summary>
        /// <returns>The interface.</returns>
        /// <param name="domain">Domain.</param>
        public MigInterface EnableInterface(string domain)
        {
            var migInterface = GetInterface(domain);
            try
            {
                if (migInterface != null)
                {
                    Log.Debug("Enabling Interface {0}", domain);
                    configuration.GetInterface(domain).IsEnabled = true;
                    migInterface.IsEnabled = true;
                    migInterface.Connect();
                }
                else
                {
                    Log.Debug("Interface not found {0}", domain);
                }
                return migInterface;
            }
            catch (Exception e)
            {
                Log.Error(e, $"Error during starting interface {domain}");
                Console.WriteLine(e);
                return migInterface;
            }
            
        }

        /// <summary>
        /// Disables the interface.
        /// </summary>
        /// <returns>The interface.</returns>
        /// <param name="domain">Domain.</param>
        public MigInterface DisableInterface(string domain)
        {
            MigInterface migInterface = GetInterface(domain);
            if (migInterface != null)
            {
                Log.Debug("Disabling Interface {0}", domain);
                configuration.GetInterface(domain).IsEnabled = false;
                migInterface.IsEnabled = false;
                migInterface.Disconnect();
            }
            else
            {
                Log.Debug("Interface not found {0}", domain);
            }
            return migInterface;
        }

        /// <summary>
        /// Gets the event.
        /// </summary>
        /// <returns>The event.</returns>
        /// <param name="domain">Domain.</param>
        /// <param name="source">Source.</param>
        /// <param name="description">Description.</param>
        /// <param name="propertyPath">Property path.</param>
        /// <param name="propertyValue">Property value.</param>
        public MigEvent GetEvent(string domain, string source, string description, string propertyPath, object propertyValue)
        {
            return new MigEvent(domain, source, description, propertyPath, propertyValue);
        }

        /// <summary>
        /// Raises the event.
        /// </summary>
        /// <param name="domain">Domain.</param>
        /// <param name="source">Source.</param>
        /// <param name="description">Description.</param>
        /// <param name="propertyPath">Property path.</param>
        /// <param name="propertyValue">Property value.</param>
        public void RaiseEvent(object sender, string domain, string source, string description, string propertyPath, object propertyValue)
        {
            RaiseEvent(sender, GetEvent(domain, source, description, propertyPath, propertyValue));
        }

        /// <summary>
        /// Raises the event.
        /// </summary>
        /// <param name="evt">Evt.</param>
        public void RaiseEvent(object sender, MigEvent evt)
        {
            OnInterfacePropertyChanged(sender, new InterfacePropertyChangedEventArgs(evt));
        }

        /// <summary>
        /// Registers the API.
        /// </summary>
        /// <param name="basePath">Base path.</param>
        /// <param name="callback">Callback.</param>
        public void RegisterApi(string basePath, Func<MigClientRequest, object> callback)
        {
            Log.Debug("Registering Dynamic API {0}", basePath);
            dynamicApi.Register(basePath, callback);
        }

        #region public Static Utility methods

        public static string JsonSerialize(object data, bool indent = false)
        {
            return Utility.Serialization.JsonSerialize(data, indent);
        }

        public static void ShellCommand(string command, string args)
        {
            var processInfo = new System.Diagnostics.ProcessStartInfo(command, args);
            processInfo.RedirectStandardOutput = false;
            processInfo.UseShellExecute = false;
            processInfo.CreateNoWindow = true;
            var process = new System.Diagnostics.Process();
            process.StartInfo = processInfo;
            try
            {
                process.Start();
            }
            catch (Exception e) 
            {
                Log.Error(e);
            }
        }

        private Type TypeLookup(string typeName, string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                var type = Type.GetType(typeName);
                return type;
            }

            if (!assemblyName.EndsWith("dll", StringComparison.OrdinalIgnoreCase))
                assemblyName = assemblyName + ".dll";

            // Look in default folder and include subfolders - thus getting plugins when migrated to and providing backwards compatability
            var filesFound = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), "../data/"), assemblyName, SearchOption.AllDirectories);

            if (filesFound.Length == 0)
            {
                throw new FileNotFoundException($"Couldn't load assembly for interface/type {typeName}", assemblyName);
            }

            XNamespace ns = "urn:schemas-microsoft-com:asm.v1";
            var namespaceManager = new XmlNamespaceManager(new NameTable());
            namespaceManager.AddNamespace("asmv1", ns.NamespaceName);

            foreach (var fileName in filesFound)
            {
                Log.Debug($"Loading {fileName}");
                var assemblyFile = Path.GetFullPath(fileName);
                var assembly = Assembly.LoadFrom(assemblyFile);
                var type = assembly.GetType(typeName);
                if (type == null)
                    continue;

                var files = Directory.EnumerateFiles(new FileInfo(assemblyFile).DirectoryName, "*.dll", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if(Path.GetFullPath(file) == assemblyFile)
                        continue;
                    Assembly.LoadFrom(Path.GetFullPath(file));
                }

                var configFile = $"{assemblyFile}.config";
                if (!File.Exists(configFile))
                    return type;

                var config = XElement.Load(File.OpenText(configFile));
                var dependentAssemblyElements = config.XPathSelectElements("//asmv1:dependentAssembly", namespaceManager).ToList();
                foreach (var dependentAssemblyElement in dependentAssemblyElements)
                {
                    var d = dependentAssemblyElement.Element(ns + "assemblyIdentity");
                    var assemblyToRedirect = d?.Attribute("name")?.Value;
                    if (assemblyToRedirect != null)
                        RedirectAssembly(assemblyToRedirect);
                }
                return type;
            }

            return null;
        }

        private static Version VersionLookup(Assembly assembly)
        {
            return assembly?.GetName().Version;
        }

        public static string GetAssemblyDirectory(Assembly assembly)
        {
            var codeBase = assembly.CodeBase;
            var uri = new UriBuilder(codeBase);
            var path = Uri.UnescapeDataString(uri.Path);
            return Path.GetDirectoryName(path);
        }

        #endregion

        #endregion

        #region Private members

        #region Utility methods

        private object TryDynamicApi(MigClientRequest request)
        {
            object response = null;
            var command = request.Command;
            var apiCommand = command.Domain + "/" + command.Address + "/" + command.Command;
            // Standard MIG API commands should respect the the form
            // <domain>/<address>/<command>[/<option_1>/../<option_n>] 
            var handler = dynamicApi.FindExact(apiCommand);
            if (handler == null)
            {
                // We didn't find a dynamic API matching standard formatted MIG API command, so we try
                // to just find any dynamic API that starts with the requested url
                handler = dynamicApi.FindMatching(command.OriginalRequest.Trim('/'));
            }
            if (handler != null)
            {
                // We found an handler for this API call, so invoke it
                response = handler(request);
            }
            // TODO: the following is old code used for HomeGenie, move to HG
            /*
            if (handler != null)
            {
                // explicit command API handlers registered in the form <domain>/<address>/<command>
                // receives only the remaining part of the request after the <command>
                var args = command.OriginalRequest.Substring(registeredApi.Length).Trim('/');
                response = handler(args);
            }
            else
            {
                handler = dynamicApi.FindMatching(command.OriginalRequest.Trim('/'));
                if (handler != null)
                {
                    // other command API handlers
                    // receives the full request string
                    response = handler(command.OriginalRequest.Trim('/'));
                }
            }
            */
            return response;
        }

        #endregion

        #region MigInterface events

        private void MigService_InterfacePropertyChanged(object sender, InterfacePropertyChangedEventArgs args)
        {
            // event propagation
            // TODO: should preserve the original "sender" ?
            OnInterfacePropertyChanged(sender, args);
        }

        private void MigService_InterfaceModulesChanged(object sender, InterfaceModulesChangedEventArgs args)
        {
            // event propagation
            // TODO: should preserve the original "sender" ?
            OnInterfaceModulesChanged(sender, args);
        }

        #endregion

        #region MigGateway events

        private void Gateway_PreProcessRequest(object sender, ProcessRequestEventArgs args)
        {
            var request = args.Request;
            // Route event
            OnPreProcessRequest(sender, request);

            if (request.Handled)
                return;

            var command = request.Command;
            if (command.Domain == "MIGService.Interfaces")
            {
                // This is a MIGService namespace Web API
                switch (command.Command)
                {
                case "IsEnabled.Set":
                    if (command.GetOption(0) == "1")
                    {
                        if (EnableInterface(command.Address) != null)
                            request.ResponseData = new ResponseStatus(Status.Ok, String.Format("Interface {0} enabled", command.Address));
                        else
                            request.ResponseData = new ResponseStatus(Status.Error, String.Format("Interface {0} not found", command.Address));
                    }
                    else
                    {
                        if (DisableInterface(command.Address) != null)
                            request.ResponseData = new ResponseStatus(Status.Ok, String.Format("Interface {0} disabled", command.Address));
                        else
                            request.ResponseData = new ResponseStatus(Status.Error, String.Format("Interface {0} not found", command.Address));
                    }
                    OnInterfacePropertyChanged(sender, new InterfacePropertyChangedEventArgs("MIGService.Interfaces", command.Address, "MIG Interface", "Status.IsEnabled", command.GetOption(0)));
                    break;
                case "IsEnabled.Get":
                    request.ResponseData = new ResponseText(configuration.GetInterface(command.Address).IsEnabled ? "1" : "0");
                    break;
                case "Options.Set":
                    {
                        var iface = GetInterface(command.Address);
                        if (iface != null)
                        {
                            iface.SetOption(command.GetOption(0), command.GetOption(1));
                            request.ResponseData = new ResponseStatus(Status.Ok, String.Format("{0} option '{1}' set to '{2}'", command.Address, command.GetOption(0), command.GetOption(1)));
                        }
                        else
                        {
                            request.ResponseData = new ResponseStatus(Status.Error, String.Format("Interface {0} not found", command.Address));
                        }
                    }
                    OnInterfacePropertyChanged(sender, new InterfacePropertyChangedEventArgs("MIGService.Interfaces", command.Address, "MIG Interface", "Options." + command.GetOption(0), command.GetOption(1)));
                    break;
                case "Options.Get":
                    {
                        var iface = GetInterface(command.Address);
                        if (iface != null)
                        {
                            string optionValue = iface.GetOption(command.GetOption(0)).Value;
                            request.ResponseData = new ResponseText(optionValue);
                        }
                        else
                        {
                            request.ResponseData = new ResponseStatus(Status.Error, String.Format("Interface {0} not found", command.Address));
                        }
                    }
                    break;
                default:
                    break;
                }
            }
            else
            {
                // Try processing as MigInterface Api or Web Service Dynamic Api
                var iface = (from miginterface in Interfaces
                    let ns = miginterface.GetType().Namespace
                    let domain = ns.Substring(ns.LastIndexOf(".") + 1) + "." + miginterface.GetType().Name
                    where (command.Domain != null && command.Domain.StartsWith(domain))
                    select miginterface).FirstOrDefault();
                if (iface != null) // && iface.IsEnabled)
                {
                    //if (iface.IsConnected)
                    //{
                        try
                        {
                            request.ResponseData = iface.InterfaceControl(command);
                        }
                        catch (Exception ex)
                        {
                            request.ResponseData = new ResponseStatus(Status.Error, JsonSerialize(ex));
                        }
                    //}
                    //else
                    //{
                    //    request.ResponseData = new ResponseStatus(Status.Error, String.Format("Interface '{0}' not connected", iface.GetDomain()));
                    //}
                }
                // Try processing as Dynamic API
                if ((request.ResponseData == null || request.ResponseData.Equals(String.Empty)))
                {
                    request.ResponseData = TryDynamicApi(request);
                }

            }

        }

        private void Gateway_PostProcessRequest(object sender, ProcessRequestEventArgs args)
        {
            var request = args.Request;
            // Route event
            OnPostProcessRequest(sender, request);
        }

        #endregion

        #region MigService Events

        protected virtual void OnPreProcessRequest(object sender, MigClientRequest request)
        {
            if (GatewayRequestPreProcess != null)
                GatewayRequestPreProcess(sender, new ProcessRequestEventArgs(request));
        }

        protected virtual void OnPostProcessRequest(object sender, MigClientRequest request)
        {
            if (GatewayRequestPostProcess != null)
                GatewayRequestPostProcess(sender, new ProcessRequestEventArgs(request));
        }

        protected virtual void OnInterfaceModulesChanged(object sender, InterfaceModulesChangedEventArgs args)
        {
            Log.Info(args.Domain);
            if (InterfaceModulesChanged != null)
            {
                InterfaceModulesChanged(sender, args);
            }
        }

        protected virtual void OnInterfacePropertyChanged(object sender, InterfacePropertyChangedEventArgs args)
        {
            Log.Info(args.EventData);
            if (InterfacePropertyChanged != null)
            {
                InterfacePropertyChanged(sender, args);
            }
            // Route event to MIG.Gateways as well
            foreach (var gateway in Gateways)
                gateway.OnInterfacePropertyChanged(sender, args);
        }

        #endregion

        #endregion

    }

}
