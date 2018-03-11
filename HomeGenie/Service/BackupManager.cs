using System;
using System.IO;
using HomeGenie.Automation;
using System.Xml.Serialization;
using System.Collections.Generic;
using HomeGenie.Data;
using HomeGenie.Service.Constants;
using MIG.Config;
using MIG;
using System.Text;
using HomeGenie.Database;

namespace HomeGenie.Service
{
    public class BackupManager
    {
        private readonly HomeGenieService _homegenie;

        public BackupManager(HomeGenieService hg)
        {
            _homegenie = hg;
        }

        public void BackupConfiguration(string archiveName)
        {
            _homegenie.UpdateProgramsDatabase();
            _homegenie.UpdateGroupsDatabase("Automation");
            _homegenie.UpdateGroupsDatabase("Control");
            _homegenie.SaveData();
            if (File.Exists(archiveName))
            {
                File.Delete(archiveName);
            }

            // Add USERSPACE automation program binaries (csharp)
            foreach (var program in _homegenie.ProgramManager.Programs)
            {
                if (program.Address >= ProgramManager.USERSPACE_PROGRAMS_START && program.Address < ProgramManager.PACKAGE_PROGRAMS_START)
                {
                    var relFile = Path.Combine("programs/", program.Address + ".dll");
                    if (File.Exists(relFile))
                    {
                        Utility.AddFileToZip(archiveName, relFile);
                    }

                    if (program.Type.ToLower() == "arduino")
                    {
                        var arduinoFolder = Path.Combine("programs", "arduino", program.Address.ToString());
                        var filePaths = Directory.GetFiles(arduinoFolder);
                        foreach (var f in filePaths)
                        {
                            Utility.AddFileToZip(archiveName, Path.Combine(arduinoFolder, Path.GetFileName(f)));
                        }
                    }
                }
            }

            // Add system config files
            Utility.AddFileToZip(archiveName, "systemconfig.xml");
            Utility.AddFileToZip(archiveName, "automationgroups.xml");
            Utility.AddFileToZip(archiveName, "modules.xml");
            Utility.AddFileToZip(archiveName, "programs.xml");
            Utility.AddFileToZip(archiveName, "scheduler.xml");
            Utility.AddFileToZip(archiveName, "groups.xml");
            Utility.AddFileToZip(archiveName, "release_info.xml");
            // Statistics db
            if (File.Exists(StatisticsRepository.StatisticsDbFile))
            {
                //homegenie.Statistics.CloseStatisticsDatabase();
                Utility.AddFileToZip(archiveName, StatisticsRepository.StatisticsDbFile);
                //homegenie.Statistics.OpenStatisticsDatabase();
            }

            // Installed packages
            if (File.Exists(PackageManager.PACKAGE_LIST_FILE))
                Utility.AddFileToZip(archiveName, PackageManager.PACKAGE_LIST_FILE);
            // Add MIG Interfaces config/data files (lib/mig/*.xml)
            var migLibFolder = Path.Combine("lib", "mig");
            if (Directory.Exists(migLibFolder))
            {
                foreach (var f in Directory.GetFiles(migLibFolder, "*.xml"))
                {
                    // exclude Pepper1 Db from backup (only the p1db_custom.xml file will be included)
                    // in the future the p1db.xml file should be moved to a different path 
                    if (Path.GetFileName(f) != "p1db.xml")
                        Utility.AddFileToZip(archiveName, f);
                }
            }
        }

        public bool RestoreConfiguration(string archiveFolder, string selectedPrograms)
        {
            // TODO: move this to a separate class file method (eg. BackupHelper.cs)
            var success = true;
            var actions = new List<Action<string, string>>
            {
                ImportAutomationGroups,
                CopySystemConfigurationFiles,
                RestoreStatisticsDb,
                RemoveOldUserPrograms,
                RestoreInstalledPackages,
                UpdateProgramDatabase,
                UpdateSystemConfig,
                RemoveOldMigInterfacesConfig,
                RestoreMigConfiguration,
                SoftReload,
                RestoreUserPrograms
            };

            foreach (var action in actions)
            {
                try
                {
                    action(archiveFolder, selectedPrograms);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    MigService.Log.Error(e);
                    success = false;
                }
            }

            _homegenie.RaiseEvent(
                Domains.HomeGenie_System,
                Domains.HomeGenie_BackupRestore,
                SourceModule.Master,
                "HomeGenie Backup Restore",
                Properties.InstallProgressMessage,
                "= Status: Backup Restore " + (success ? "Succesful" : "Errors")
            );
            _homegenie.SaveData();

            return success;
        }

        private void RestoreStatisticsDb(string archiveFolder, string selectedPrograms)
        {
            // Statistics db
            if (File.Exists(Path.Combine(archiveFolder, StatisticsRepository.StatisticsDbFile)))
            {
                File.Copy(Path.Combine(archiveFolder, StatisticsRepository.StatisticsDbFile),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, StatisticsRepository.StatisticsDbFile), true);
                _homegenie.RaiseEvent(
                    Domains.HomeGenie_System,
                    Domains.HomeGenie_BackupRestore,
                    SourceModule.Master,
                    "HomeGenie Backup Restore",
                    Properties.InstallProgressMessage,
                    "= Restored: Statistics Database"
                );
            }
        }

        private void RemoveOldUserPrograms(string archiveFolder, string selectedPrograms)
        {
            // Remove all old non-system programs
            var rp = new List<ProgramBlock>();
            foreach (var program in _homegenie.ProgramManager.Programs)
            {
                if (program.Address >= ProgramManager.USERSPACE_PROGRAMS_START)
                    rp.Add(program);
            }

            foreach (var program in rp)
            {
                _homegenie.ProgramManager.ProgramRemove(program);
                _homegenie.RaiseEvent(
                    Domains.HomeGenie_System,
                    Domains.HomeGenie_BackupRestore,
                    SourceModule.Master,
                    "HomeGenie Backup Restore",
                    Properties.InstallProgressMessage,
                    "= Removed: Program '" + program.Name + "' (" + program.Address + ")"
                );
            }
        }

        private void RestoreInstalledPackages(string archiveFolder, string selectedPrograms)
        {
            // Restore installed packages
            if (File.Exists(Path.Combine(archiveFolder, PackageManager.PACKAGE_LIST_FILE)))
            {
                File.Copy(Path.Combine(archiveFolder, PackageManager.PACKAGE_LIST_FILE), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PackageManager.PACKAGE_LIST_FILE),
                    true);
                // Restore packages from "installed_packages.json"
                var installFolder = Path.Combine(archiveFolder, "pkg");
                var pkgList = _homegenie.PackageManager.LoadInstalledPackages();
                foreach (var pkg in pkgList)
                {
                    _homegenie.PackageManager.InstallPackage(pkg.folder_url.ToString(), installFolder);
                }
            }
        }

        private void UpdateProgramDatabase(string archiveFolder, string selectedPrograms)
        {
            // Update program database after package restore
            _homegenie.UpdateProgramsDatabase();
        }

        private void RemoveOldMigInterfacesConfig(string archiveFolder, string selectedPrograms)
        {
            // Remove old MIG Interfaces config/data files (lib/mig/*.xml)
            var migLibFolder = Path.Combine("lib", "mig");
            if (Directory.Exists(migLibFolder))
            {
                foreach (var f in Directory.GetFiles(migLibFolder, "*.xml"))
                {
                    File.Delete(f);
                    _homegenie.RaiseEvent(
                        Domains.HomeGenie_System,
                        Domains.HomeGenie_BackupRestore,
                        SourceModule.Master,
                        "HomeGenie Backup Restore",
                        Properties.InstallProgressMessage,
                        "= Removed: MIG Data File '" + f + "'"
                    );
                }
            }
        }

        private void RestoreMigConfiguration(string archiveFolder, string selectedPrograms)
        {
            // Restore MIG configuration/data files if present (from backup folder lib/mig/*.xml)
            var migLibFolder = Path.Combine(archiveFolder, "lib", "mig");
            if (Directory.Exists(migLibFolder))
            {
                foreach (var f in Directory.GetFiles(migLibFolder, "*.xml"))
                {
                    File.Copy(f, Path.Combine("lib", "mig", Path.GetFileName(f)), true);
                    _homegenie.RaiseEvent(
                        Domains.HomeGenie_System,
                        Domains.HomeGenie_BackupRestore,
                        SourceModule.Master,
                        "HomeGenie Backup Restore",
                        Properties.InstallProgressMessage,
                        "= Restored: '" + Path.Combine("lib", "mig", Path.GetFileName(f)) + "'"
                    );
                }
            }
        }

        private void SoftReload(string archiveFolder, string selectedPrograms)
        {
            // Soft-reload system configuration from newely restored files and save config
            _homegenie.SoftReload();
        }

        private void RestoreUserPrograms(string archiveFolder, string selectedPrograms)
        {
            // Restore user-space automation programs
            var serializer = new XmlSerializer(typeof(List<ProgramBlock>));
            var reader = new StreamReader(Path.Combine(archiveFolder, "programs.xml"));
            var newProgramsData = (List<ProgramBlock>) serializer.Deserialize(reader);
            reader.Close();
            foreach (var program in newProgramsData)
            {
                var currentProgram = _homegenie.ProgramManager.Programs.Find(p => p.Address == program.Address);
                program.IsRunning = false;
                // Only restore user space programs
                if (selectedPrograms.Contains("," + program.Address + ",") && program.Address >= ProgramManager.USERSPACE_PROGRAMS_START &&
                    program.Address < ProgramManager.PACKAGE_PROGRAMS_START)
                {
                    var oldPid = program.Address;
                    if (currentProgram == null)
                    {
                        var newPid = currentProgram != null && currentProgram.Address == program.Address
                            ? _homegenie.ProgramManager.GeneratePid()
                            : program.Address;
                        try
                        {
                            File.Copy(Path.Combine(archiveFolder, "programs", program.Address + ".dll"),
                                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "programs", newPid + ".dll"), true);
                        }
                        catch
                        {
                        }

                        program.Address = newPid;
                        _homegenie.ProgramManager.ProgramAdd(program);
                        _homegenie.RaiseEvent(
                            Domains.HomeGenie_System,
                            Domains.HomeGenie_BackupRestore,
                            SourceModule.Master,
                            "HomeGenie Backup Restore",
                            Properties.InstallProgressMessage,
                            "= Added: Program '" + program.Name + "' (" + program.Address + ")"
                        );
                    }
                    else
                    {
                        _homegenie.ProgramManager.ProgramRemove(currentProgram);
                        try
                        {
                            File.Copy(Path.Combine(archiveFolder, "programs", program.Address + ".dll"),
                                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "programs", program.Address + ".dll"), true);
                        }
                        catch
                        {
                        }

                        _homegenie.ProgramManager.ProgramAdd(program);
                        _homegenie.RaiseEvent(
                            Domains.HomeGenie_System,
                            Domains.HomeGenie_BackupRestore,
                            SourceModule.Master,
                            "HomeGenie Backup Restore",
                            Properties.InstallProgressMessage,
                            "= Replaced: Program '" + program.Name + "' (" + program.Address + ")"
                        );
                    }

                    // Restore Arduino program folder ...
                    // TODO: this is untested yet...
                    if (program.Type.ToLower() == "arduino")
                    {
                        var sourceFolder = Path.Combine(archiveFolder, "programs", "arduino", oldPid.ToString());
                        var arduinoFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "programs", "arduino", program.Address.ToString());
                        if (Directory.Exists(arduinoFolder))
                            Directory.Delete(arduinoFolder, true);
                        Directory.CreateDirectory(arduinoFolder);
                        foreach (var newPath in Directory.GetFiles(sourceFolder))
                        {
                            File.Copy(newPath, newPath.Replace(sourceFolder, arduinoFolder), true);
                        }
                    }
                }
                else if (currentProgram != null && (program.Address < ProgramManager.USERSPACE_PROGRAMS_START || program.Address >= ProgramManager.PACKAGE_PROGRAMS_START))
                {
                    // Only restore Enabled/Disabled status of system programs and packages
                    currentProgram.IsEnabled = program.IsEnabled;
                }
            }

            _homegenie.UpdateProgramsDatabase();
        }


        private void ImportAutomationGroups(string archiveFolder, string selectedPrograms)
        {
            // Import automation groups
            var serializer = new XmlSerializer(typeof(List<Group>));
            var reader = new StreamReader(Path.Combine(archiveFolder, "automationgroups.xml"));
            var automationGroups = (List<Group>) serializer.Deserialize(reader);
            reader.Close();
            foreach (var automationGroup in automationGroups)
            {
                if (_homegenie.AutomationGroups.Find(g => g.Name == automationGroup.Name) == null)
                {
                    _homegenie.AutomationGroups.Add(automationGroup);
                    _homegenie.RaiseEvent(
                        Domains.HomeGenie_System,
                        Domains.HomeGenie_BackupRestore,
                        SourceModule.Master,
                        "HomeGenie Backup Restore",
                        Properties.InstallProgressMessage,
                        "= Added: Automation Group '" + automationGroup.Name + "'"
                    );
                }
            }

            _homegenie.UpdateGroupsDatabase("Automation");
        }

        private void CopySystemConfigurationFiles(string archiveFolder, string selectedPrograms)
        {
            // Copy system configuration files
            File.Copy(Path.Combine(archiveFolder, "groups.xml"), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "groups.xml"), true);
            _homegenie.RaiseEvent(
                Domains.HomeGenie_System,
                Domains.HomeGenie_BackupRestore,
                SourceModule.Master,
                "HomeGenie Backup Restore",
                Properties.InstallProgressMessage,
                "= Restored: Control Groups"
            );
            File.Copy(Path.Combine(archiveFolder, "modules.xml"), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modules.xml"), true);
            _homegenie.RaiseEvent(
                Domains.HomeGenie_System,
                Domains.HomeGenie_BackupRestore,
                SourceModule.Master,
                "HomeGenie Backup Restore",
                Properties.InstallProgressMessage,
                "= Restored: Modules"
            );
            File.Copy(Path.Combine(archiveFolder, "scheduler.xml"), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scheduler.xml"), true);
            _homegenie.RaiseEvent(
                Domains.HomeGenie_System,
                Domains.HomeGenie_BackupRestore,
                SourceModule.Master,
                "HomeGenie Backup Restore",
                Properties.InstallProgressMessage,
                "= Restored: Scheduler Events"
            );
        }

        // Backward compatibility method for HG < 1.1
        private void UpdateSystemConfig(string configPath, string selectedPrograms)
        {
            var configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "systemconfig.xml");
            var configText = File.ReadAllText(Path.Combine(configPath, "systemconfig.xml"));
            if (configText.IndexOf("<ServicePort>") > 0)
            {
                configText = configText.Replace("SystemConfiguration", "SystemConfiguration_1_0");
                configText = configText.Replace("HomeGenieConfiguration", "HomeGenieConfiguration_1_0");
                // This is old configuration file from HG < 1.1
                SystemConfiguration_1_0 oldConfig;
                var newConfig = new SystemConfiguration();
                try
                {
                    // Load old config
                    var serializerOld = new XmlSerializer(typeof(SystemConfiguration_1_0));
                    using (var reader = new StringReader(configText))
                        oldConfig = (SystemConfiguration_1_0) serializerOld.Deserialize(reader);
                    // Copy setting to the new config format
                    newConfig.HomeGenie.Settings = oldConfig.HomeGenie.Settings;
                    newConfig.HomeGenie.SystemName = oldConfig.HomeGenie.SystemName;
                    newConfig.HomeGenie.Location = oldConfig.HomeGenie.Location;
                    newConfig.HomeGenie.GUID = oldConfig.HomeGenie.GUID;
                    newConfig.HomeGenie.EnableLogFile = oldConfig.HomeGenie.EnableLogFile;
                    newConfig.HomeGenie.Statistics = new HomeGenieConfiguration.StatisticsConfiguration();
                    newConfig.HomeGenie.Statistics.MaxDatabaseSizeMBytes = oldConfig.HomeGenie.Statistics.MaxDatabaseSizeMBytes;
                    newConfig.HomeGenie.Statistics.StatisticsTimeResolutionSeconds = oldConfig.HomeGenie.Statistics.StatisticsTimeResolutionSeconds;
                    newConfig.HomeGenie.Statistics.StatisticsUIRefreshSeconds = oldConfig.HomeGenie.Statistics.StatisticsUIRefreshSeconds;
                    var webGateway = new Gateway {Name = "WebServiceGateway", IsEnabled = true};
                    webGateway.Options = new List<Option>
                    {
                        new Option("BaseUrl", "/hg/html"),
                        new Option("HomePath", "html"),
                        new Option("Host", oldConfig.HomeGenie.ServiceHost),
                        new Option("Port", oldConfig.HomeGenie.ServicePort.ToString()),
                        new Option("Username", "admin"),
                        new Option("Password", oldConfig.HomeGenie.UserPassword),
                        new Option("HttpCacheIgnore.1", "^.*\\/pages\\/control\\/widgets\\/.*\\.(js|html)$"),
                        new Option("HttpCacheIgnore.2", "^.*\\/html\\/index.html"),
                        new Option("UrlAlias.1", "api/HomeAutomation.HomeGenie/Logging/RealTime.EventStream:events"),
                        new Option("UrlAlias.2",
                            "hg/html/pages/control/widgets/homegenie/generic/images/socket_on.png:hg/html/pages/control/widgets/homegenie/generic/images/switch_on.png"),
                        new Option("UrlAlias.3",
                            "hg/html/pages/control/widgets/homegenie/generic/images/socket_off.png:hg/html/pages/control/widgets/homegenie/generic/images/switch_off.png"),
                        new Option("UrlAlias.4",
                            "hg/html/pages/control/widgets/homegenie/generic/images/siren.png:hg/html/pages/control/widgets/homegenie/generic/images/siren_on.png"),
                        new Option("EnableFileCaching", "false")
                    };

                    // TODO: EnableFileCaching value should be read from oldConfig.MIGService.EnableWebCache
                    newConfig.MigService.Gateways.Add(webGateway);
                    newConfig.MigService.Interfaces = oldConfig.MIGService.Interfaces;
                    foreach (var iface in newConfig.MigService.Interfaces)
                    {
                        if (iface.Domain == "HomeAutomation.ZWave")
                            iface.AssemblyName = "MIG.HomeAutomation.dll";
                        if (iface.Domain == "HomeAutomation.Insteon")
                            iface.AssemblyName = "MIG.HomeAutomation.dll";
                        if (iface.Domain == "HomeAutomation.X10")
                            iface.AssemblyName = "MIG.HomeAutomation.dll";
                        if (iface.Domain == "HomeAutomation.W800RF")
                            iface.AssemblyName = "MIG.HomeAutomation.dll";
                        if (iface.Domain == "Controllers.LircRemote")
                            iface.AssemblyName = "MIG.Controllers.dll";
                        if (iface.Domain == "Media.CameraInput")
                            iface.AssemblyName = "MIG.Media.dll";
                        if (iface.Domain == "Protocols.UPnP")
                            iface.AssemblyName = "MIG.Protocols.dll";
                    }

                    // Check for lircconfig.xml
                    if (File.Exists(Path.Combine(configPath, "lircconfig.xml")))
                    {
                        File.Copy(Path.Combine(configPath, "lircconfig.xml"), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", "mig", "lircconfig.xml"), true);
                    }

                    // Update configuration file
                    if (File.Exists(configFile))
                    {
                        File.Delete(configFile);
                    }

                    var ws = new System.Xml.XmlWriterSettings
                    {
                        Indent = true,
                        Encoding = Encoding.UTF8
                    };
                    var x = new XmlSerializer(newConfig.GetType());
                    var wri = System.Xml.XmlWriter.Create(configFile, ws);
                    x.Serialize(wri, newConfig);
                    wri.Close();
                }
                catch (Exception e)
                {
                    MigService.Log.Error(e);
                }
            }
            else
            {
                // HG >= 1.1
                File.Copy(Path.Combine(configPath, "systemconfig.xml"), configFile, true);
            }
        }

    }
}
