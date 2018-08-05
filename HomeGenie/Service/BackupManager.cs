using System;
using System.IO;
using HomeGenie.Automation;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Linq;
using HomeGenie.Data;
using HomeGenie.Service.Constants;
using HomeGenie.Utils;
using MIG;
using MIG.Config;

namespace HomeGenie.Service
{
    public class BackupManager
    {
        private readonly HomeGenieService _homegenie;
        public const string RestoreTempFolder = "_tmp_backup";

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
                        ArchiveHelper.AddFileToZip(archiveName, relFile);
                    }

                    if (program.Type.ToLower() == "arduino")
                    {
                        var arduinoFolder = Path.Combine(FilePaths.ProgramsFolder, "arduino", program.Address.ToString());
                        var filePaths = Directory.GetFiles(arduinoFolder);
                        foreach (var f in filePaths)
                        {
                            ArchiveHelper.AddFileToZip(archiveName, Path.Combine(arduinoFolder, Path.GetFileName(f)));
                        }
                    }
                }
            }

            ArchiveHelper.AddFolderToZip(archiveName, FilePaths.DataFolder);
        }

        public bool RestoreConfiguration(string archiveFolder, string selectedPrograms)
        {
            var success = Directory.Exists(Path.Combine(archiveFolder, "interfaces"))
                ? RestoreNewConfiguration(archiveFolder, selectedPrograms)
                : RestoreOldConfiguration(archiveFolder, selectedPrograms);

            Directory.Delete(archiveFolder, true);
            return success;
        }

        private bool RestoreNewConfiguration(string archiveFolder, string selectedPrograms)
        {
            var oldConfigFile = Path.Combine(archiveFolder, FilePaths.SystemConfigFileName);
            EnsureSystemConfigIsSafeForDocker(oldConfigFile);
            Program.Quit(true, false);
            return true;
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

            RaiseEvent($"= Status: Backup Restore {(success ? "Succesful" : "Errors")}");
            _homegenie.SaveData();

            return success;
        }

        private void RestoreStatisticsDb(string archiveFolder, string selectedPrograms)
        {
            // Statistics db
            if (!File.Exists(Path.Combine(archiveFolder, FilePaths.StatisticsDbFileName)))
                return;

            File.Copy(Path.Combine(archiveFolder, FilePaths.StatisticsDbFileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FilePaths.StatisticsDbFilePath), true);
            RaiseEvent("= Restored: Statistics Database");
        }

        private void RemoveOldUserPrograms(string archiveFolder, string selectedPrograms)
        {
            // Remove all old non-system programs
            var programsToRemove = _homegenie.ProgramManager.Programs.Where(x => x.Address >= ProgramManager.USERSPACE_PROGRAMS_START).ToList();
            foreach (var program in programsToRemove)
            {
                _homegenie.ProgramManager.ProgramRemove(program);
                RaiseEvent($"= Removed: Program \'{program.Name}\' ({program.Address})");
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
            var pkgList = _homegenie.PackageManager.LoadInstalledPackages();
            foreach (var pkg in pkgList)
            {
                _homegenie.PackageManager.InstallPackage(pkg.SourceUrl);
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
                    RaiseEvent($"= Removed: MIG Data File \'{f}\'");
                }
            }
        }

        private void RestoreMigConfiguration(string archiveFolder, string selectedPrograms)
        {
            // Move all known interfaces configs from the old backup to the appropriate folders
            var knownConfigurationFiles = new Dictionary<string, string[]>
            {
                {"ZWave", new[] {"p1db_custom.xml", "zwavenodes.xml"}},
                {"Controllers.LircRemote", new[] {"lircconfig.xml", "lircremotes.xml"}}
            };

            var migLibFolder = Path.Combine(archiveFolder, "lib", "mig");
            if (!Directory.Exists(migLibFolder))
                return;

            foreach (var f in Directory.GetFiles(migLibFolder, "*.xml"))
            {
                var fileName = Path.GetFileName(f);
                var iface = knownConfigurationFiles.FirstOrDefault(x => x.Value.Contains(fileName));
                if (iface.Key == null)
                    continue;

                if (!Directory.Exists(Path.Combine(FilePaths.InterfacesFolder, iface.Key)))
                    continue;

                File.Copy(f, Path.Combine(FilePaths.InterfacesFolder, iface.Key, fileName), true);
                RaiseEvent($"= Restored: \'{Path.Combine("lib", "mig", Path.GetFileName(f))}\'");
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
                    if (currentProgram != null)
                        _homegenie.ProgramManager.ProgramRemove(currentProgram);

                    var targetFile = Path.Combine(FilePaths.ProgramsFolder, program.Address + ".dll");
                    try
                    {
                        File.Copy(Path.Combine(archiveFolder, "programs", program.Address + ".dll"), targetFile, true);
                    }
                    catch (Exception e)
                    {
                        var errorMessage = e.Message + Environment.NewLine + e.StackTrace + Environment.NewLine + "Target file: " + targetFile;
                        RaiseEvent($"= Error copying program: {program.Address}. Details: " + errorMessage);
                    }

                    _homegenie.ProgramManager.ProgramAdd(program);
                    RaiseEvent($"= Added: Program \'{program.Name}\' ({program.Address})");

                    // Restore Arduino program folder ...
                    // TODO: this is untested yet...
                    if (program.Type.ToLower() == "arduino")
                    {
                        var sourceFolder = Path.Combine(archiveFolder, "programs", "arduino", oldPid.ToString());
                        var arduinoFolder = Path.Combine(FilePaths.ProgramsFolder, "arduino", program.Address.ToString());
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
                    RaiseEvent($"= Added: Automation Group \'{automationGroup.Name}\'");
                }
            }

            _homegenie.UpdateAutomationGroupsDatabase();
        }

        private void CopySystemConfigurationFiles(string archiveFolder, string selectedPrograms)
        {
            // Copy system configuration files
            File.Copy(Path.Combine(archiveFolder, FilePaths.GroupsFileName), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FilePaths.GroupsFilePath), true);
            RaiseEvent("= Restored: Control Groups");

            File.Copy(Path.Combine(archiveFolder, FilePaths.ModulesFileName), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FilePaths.ModulesFilePath), true);
            RaiseEvent("= Restored: Modules");

            File.Copy(Path.Combine(archiveFolder, FilePaths.SchedulerFileName), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FilePaths.SchedulerFilePath), true);
            RaiseEvent("= Restored: Scheduler Events");
        }

        private void UpdateSystemConfig(string archiveFolder, string selectedPrograms)
        {
            var oldConfigFile = Path.Combine(archiveFolder, FilePaths.SystemConfigFileName);
            var newConfigFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FilePaths.SystemConfigFilePath);
            EnsureSystemConfigIsSafeForDocker(oldConfigFile);
            FixOldInterfaceAssembliesNames(oldConfigFile);
            File.Copy(oldConfigFile, newConfigFile, true);
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

        private void FixOldInterfaceAssembliesNames(string oldConfigFile)
        {
            SystemConfiguration systemConfiguration;
            var serializer = new XmlSerializer(typeof(SystemConfiguration));
            using (var reader = new StreamReader(oldConfigFile))
            {
                systemConfiguration = (SystemConfiguration) serializer.Deserialize(reader);
            }

            var zWaveInterface = systemConfiguration.MigService.Interfaces.FirstOrDefault(x => x.Domain == "HomeAutomation.ZWave");
            if (zWaveInterface != null)
            {
                zWaveInterface.AssemblyName = "MIG.Interfaces.HomeAutomation.ZWave.dll";
            }

            var x10Interface = systemConfiguration.MigService.Interfaces.FirstOrDefault(x => x.Domain == "HomeAutomation.X10");
            if (x10Interface != null)
            {
                x10Interface.AssemblyName = "MIG.Interfaces.HomeAutomation.X10.dll";
            }

            var upnpInterface = systemConfiguration.MigService.Interfaces.FirstOrDefault(x => x.Domain == "Protocols.UPnP");
            if (upnpInterface != null)
            {
                upnpInterface.AssemblyName = "MIG.Interfaces.Protocols.UPnP.dll";
            }

            systemConfiguration.Update(oldConfigFile);
        }

        private void RaiseEvent(string message)
        {
            _homegenie.RaiseEvent(
                Domains.HomeGenie_System,
                Domains.HomeGenie_BackupRestore,
                SourceModule.Master,
                "HomeGenie Backup Restore",
                Properties.InstallProgressMessage,
                message
            );
        }
    }
}
