using System;
using System.IO;
using HomeGenie.Automation;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Linq;
using HomeGenie.Data;
using HomeGenie.Service.Constants;
using MIG;
using HomeGenie.Database;
using MIG.Config;

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
            _homegenie.UpdateAutomationGroupsDatabase();
            _homegenie.UpdateGroupsDatabase();
            _homegenie.SaveData();
            if (File.Exists(archiveName))
                File.Delete(archiveName);

            // Add USERSPACE automation program binaries (csharp)
            foreach (var program in _homegenie.ProgramManager.Programs)
            {
                if (program.Address >= ProgramManager.USERSPACE_PROGRAMS_START &&
                    program.Address < ProgramManager.PACKAGE_PROGRAMS_START)
                {
                    //var relFile = Path.Combine("programs/", program.Address + ".dll");
                    var relFile = Path.Combine(FilePaths.ProgramsFolder, program.Address + ".dll");
                    if (File.Exists(relFile))
                    {
                        Utility.AddFileToZip(archiveName, relFile);
                    }

                    if (program.Type.ToLower() == "arduino")
                    {
                        var arduinoFolder = Path.Combine(FilePaths.ProgramsFolder, "arduino", program.Address.ToString());
                        var filePaths = Directory.GetFiles(arduinoFolder);
                        foreach (var f in filePaths)
                        {
                            Utility.AddFileToZip(archiveName, Path.Combine(arduinoFolder, Path.GetFileName(f)));
                        }
                    }
                }
            }

            // Add system config files
            Utility.AddFileToZip(archiveName, FilePaths.SystemConfigFileName);
            Utility.AddFileToZip(archiveName, FilePaths.AutomationProgramsFileName);
            Utility.AddFileToZip(archiveName, FilePaths.ModulesFileName);
            Utility.AddFileToZip(archiveName, FilePaths.ProgramsFileName);
            Utility.AddFileToZip(archiveName, FilePaths.SchedulerFileName);
            Utility.AddFileToZip(archiveName, FilePaths.GroupsFileName);
            Utility.AddFileToZip(archiveName, FilePaths.ReleaseInfoFileName);
            // Statistics db
            if (File.Exists(StatisticsRepository.StatisticsDbFile))
            {
                //homegenie.Statistics.CloseStatisticsDatabase();
                Utility.AddFileToZip(archiveName, StatisticsRepository.StatisticsDbFile);
                //homegenie.Statistics.OpenStatisticsDatabase();
            }

            // Installed packages
            if (File.Exists(FilePaths.InstalledPackagesFilePath))
                Utility.AddFileToZip(archiveName, FilePaths.InstalledPackagesFileName);

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
            return Directory.Exists(Path.Combine(archiveFolder, "gateways"))
                ? RestoreNewConfiguration(archiveFolder, selectedPrograms)
                : RestoreOldConfiguration(archiveFolder, selectedPrograms);
        }

        private bool RestoreNewConfiguration(string archiveFolder, string selectedPrograms)
        {
            throw new NotImplementedException();
        }

        private bool RestoreOldConfiguration(string archiveFolder, string selectedPrograms)
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
            var programsToRemove = _homegenie.ProgramManager.Programs.Where(x => x.Address >= ProgramManager.USERSPACE_PROGRAMS_START).ToList();
            foreach (var program in programsToRemove)
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
            if (!File.Exists(Path.Combine(archiveFolder, FilePaths.InstalledPackagesFileName)))
                return;

            File.Copy(
                Path.Combine(archiveFolder, FilePaths.InstalledPackagesFileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FilePaths.InstalledPackagesFilePath),
                true);

            // Restore packages from "installed_packages.json"
            var installFolder = Path.Combine(archiveFolder, "pkg");
            var pkgList = _homegenie.PackageManager.LoadInstalledPackages();
            foreach (var pkg in pkgList)
            {
                _homegenie.PackageManager.InstallPackage(pkg.folder_url.ToString(), installFolder); // TODO HGBE-10 - check file paths
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
            // TODO HGBE-10 Move all known interfaces configs and binaries to the appropriate folders
            /*if (iface.Domain == "HomeAutomation.ZWave")
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
                            iface.AssemblyName = "MIG.Protocols.dll";*/

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
            var reader = new StreamReader(Path.Combine(archiveFolder, FilePaths.AutomationProgramsFileName));
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

            _homegenie.UpdateAutomationGroupsDatabase();
        }

        private void CopySystemConfigurationFiles(string archiveFolder, string selectedPrograms)
        {
            // Copy system configuration files
            File.Copy(Path.Combine(archiveFolder, FilePaths.GroupsFileName), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FilePaths.GroupsFilePath), true);
            _homegenie.RaiseEvent(
                Domains.HomeGenie_System,
                Domains.HomeGenie_BackupRestore,
                SourceModule.Master,
                "HomeGenie Backup Restore",
                Properties.InstallProgressMessage,
                "= Restored: Control Groups"
            );
            File.Copy(Path.Combine(archiveFolder, FilePaths.ModulesFileName), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FilePaths.ModulesFilePath), true);
            _homegenie.RaiseEvent(
                Domains.HomeGenie_System,
                Domains.HomeGenie_BackupRestore,
                SourceModule.Master,
                "HomeGenie Backup Restore",
                Properties.InstallProgressMessage,
                "= Restored: Modules"
            );
            File.Copy(Path.Combine(archiveFolder, FilePaths.SchedulerFileName), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FilePaths.SchedulerFilePath), true);
            _homegenie.RaiseEvent(
                Domains.HomeGenie_System,
                Domains.HomeGenie_BackupRestore,
                SourceModule.Master,
                "HomeGenie Backup Restore",
                Properties.InstallProgressMessage,
                "= Restored: Scheduler Events"
            );
        }

        private void UpdateSystemConfig(string archiveFolder, string selectedPrograms)
        {
            var configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FilePaths.SystemConfigFilePath);
            EnsureSystemConfigIsSafeForDocker(configFile);
            File.Copy(Path.Combine(archiveFolder, FilePaths.SystemConfigFileName), configFile, true);
        }

        private void EnsureSystemConfigIsSafeForDocker(string oldConfigFile)
        {
            var isDockerInstallation = Environment.GetEnvironmentVariable(EnvVars.HgbeDocker);
            if (string.IsNullOrEmpty(isDockerInstallation))
                return;

            SystemConfiguration systemConfiguration;
            var serializer = new XmlSerializer(typeof(SystemConfiguration));
            using (var reader = new StreamReader(oldConfigFile))
            {
                systemConfiguration = (SystemConfiguration) serializer.Deserialize(reader);
            }

            var webServiceGateway = systemConfiguration.MigService.GetGateway("WebServiceGateway");
            if (webServiceGateway == null)
                return;

            var portOption = webServiceGateway.Options.SingleOrDefault(x => x.Name == "Port");
            if (portOption == null)
                webServiceGateway.Options.Add(new Option {Name = "Port", Value = "80"});
            else
                portOption.Value = "80";
            systemConfiguration.Update(oldConfigFile);
        }
    }
}
