﻿/*
    This file is part of HomeGenie Project source code.

    HomeGenie is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    HomeGenie is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with HomeGenie.  If not, see <http://www.gnu.org/licenses/>.  
*/

/*
*     Author: Generoso Martello <gene@homegenie.it>
*     Project Homepage: http://github.com/Bounz/HomeGenie-BE
*/

using System;
using System.IO;
using HomeGenie.Automation;
using HomeGenie.Service.Logging;
using System.Xml.Serialization;
using System.Collections.Generic;
using HomeGenie.Data;
using HomeGenie.Service.Constants;
using MIG.Config;
using MIG;
using System.Text;

namespace HomeGenie.Service
{
    public class BackupManager
    {
        private HomeGenieService homegenie;

        public BackupManager(HomeGenieService hg)
        {
            homegenie = hg;
        }

        public void BackupConfiguration(string archiveName)
        {
            homegenie.UpdateProgramsDatabase();
            homegenie.UpdateGroupsDatabase("Automation");
            homegenie.UpdateGroupsDatabase("Control");
            homegenie.SaveData();
            if (File.Exists(archiveName))
            {
                File.Delete(archiveName);
            }
            // Add USERSPACE automation program binaries (csharp)
            foreach (var program in homegenie.ProgramManager.Programs)
            {
                if (program.Address >= ProgramManager.USERSPACE_PROGRAMS_START && program.Address < ProgramManager.PACKAGE_PROGRAMS_START)
                {
                    string relFile = Path.Combine("programs/", program.Address + ".dll");
                    if (File.Exists(relFile))
                    {
                        Utility.AddFileToZip(archiveName, relFile);
                    }
                    if (program.Type.ToLower() == "arduino")
                    {
                        string arduinoFolder = Path.Combine("programs", "arduino", program.Address.ToString());
                        string[] filePaths = Directory.GetFiles(arduinoFolder);
                        foreach (string f in filePaths)
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
            if (File.Exists(StatisticsLogger.STATISTICS_DB_FILE))
            {
                homegenie.Statistics.CloseStatisticsDatabase();
                Utility.AddFileToZip(archiveName, StatisticsLogger.STATISTICS_DB_FILE);
                homegenie.Statistics.OpenStatisticsDatabase();
            }
            // Installed packages
            if (File.Exists(PackageManager.PACKAGE_LIST_FILE))
                Utility.AddFileToZip(archiveName, PackageManager.PACKAGE_LIST_FILE);
            // Add MIG Interfaces config/data files (lib/mig/*.xml)
            string migLibFolder = Path.Combine("lib", "mig");
            if (Directory.Exists(migLibFolder))
            {
                foreach (string f in Directory.GetFiles(migLibFolder, "*.xml"))
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
            bool success = true;
            // Import automation groups
            var serializer = new XmlSerializer(typeof(List<Group>));
            var reader = new StreamReader(Path.Combine(archiveFolder, "automationgroups.xml"));
            var automationGroups = (List<Group>)serializer.Deserialize(reader);
            reader.Close();
            foreach (var automationGroup in automationGroups)
            {
                if (homegenie.AutomationGroups.Find(g => g.Name == automationGroup.Name) == null)
                {
                    homegenie.AutomationGroups.Add(automationGroup);
                    homegenie.RaiseEvent(
                        Domains.HomeGenie_System,
                        Domains.HomeGenie_BackupRestore,
                        SourceModule.Master,
                        "HomeGenie Backup Restore",
                        Properties.InstallProgressMessage,
                        "= Added: Automation Group '" + automationGroup.Name + "'"
                    );
                }
            }
            homegenie.UpdateGroupsDatabase("Automation");
            // Copy system configuration files
            File.Copy(Path.Combine(archiveFolder, "groups.xml"), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "groups.xml"), true);
            homegenie.RaiseEvent(
                Domains.HomeGenie_System,
                Domains.HomeGenie_BackupRestore,
                SourceModule.Master,
                "HomeGenie Backup Restore",
                Properties.InstallProgressMessage,
                "= Restored: Control Groups"
            );
            File.Copy(Path.Combine(archiveFolder, "modules.xml"), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modules.xml"), true);
            homegenie.RaiseEvent(
                Domains.HomeGenie_System,
                Domains.HomeGenie_BackupRestore,
                SourceModule.Master,
                "HomeGenie Backup Restore",
                Properties.InstallProgressMessage,
                "= Restored: Modules"
            );
            File.Copy(Path.Combine(archiveFolder, "scheduler.xml"), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scheduler.xml"), true);
            homegenie.RaiseEvent(
                Domains.HomeGenie_System,
                Domains.HomeGenie_BackupRestore,
                SourceModule.Master,
                "HomeGenie Backup Restore",
                Properties.InstallProgressMessage,
                "= Restored: Scheduler Events"
            );
            // Statistics db
            if (File.Exists(Path.Combine(archiveFolder, StatisticsLogger.STATISTICS_DB_FILE)))
            {
                File.Copy(Path.Combine(archiveFolder, StatisticsLogger.STATISTICS_DB_FILE), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, StatisticsLogger.STATISTICS_DB_FILE), true);
                homegenie.RaiseEvent(
                    Domains.HomeGenie_System,
                    Domains.HomeGenie_BackupRestore,
                    SourceModule.Master,
                    "HomeGenie Backup Restore",
                    Properties.InstallProgressMessage,
                    "= Restored: Statistics Database"
                );
            }
            // Remove all old non-system programs
            var rp = new List<ProgramBlock>();
            foreach (var program in homegenie.ProgramManager.Programs)
            {
                if (program.Address >= ProgramManager.USERSPACE_PROGRAMS_START)
                    rp.Add(program);
            }
            foreach (var program in rp)
            {
                homegenie.ProgramManager.ProgramRemove(program);
                homegenie.RaiseEvent(
                    Domains.HomeGenie_System,
                    Domains.HomeGenie_BackupRestore,
                    SourceModule.Master,
                    "HomeGenie Backup Restore",
                    Properties.InstallProgressMessage,
                    "= Removed: Program '" + program.Name + "' (" + program.Address + ")"
                );
            }
            // Restore installed packages
            if (File.Exists(Path.Combine(archiveFolder, PackageManager.PACKAGE_LIST_FILE)))
            {
                File.Copy(Path.Combine(archiveFolder, PackageManager.PACKAGE_LIST_FILE), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PackageManager.PACKAGE_LIST_FILE), true);
                // Restore packages from "installed_packages.json"
                string installFolder = Path.Combine(archiveFolder, "pkg");
                List<dynamic> pkgList = homegenie.PackageManager.LoadInstalledPackages();
                foreach (var pkg in pkgList)
                {
                    success = success && homegenie.PackageManager.InstallPackage(pkg.folder_url.ToString(), installFolder);
                }
            }
            // Update program database after package restore
            homegenie.UpdateProgramsDatabase();
            // Update system config
            UpdateSystemConfig(archiveFolder);
            // Remove old MIG Interfaces config/data files (lib/mig/*.xml)
            string migLibFolder = Path.Combine("lib", "mig");
            if (Directory.Exists(migLibFolder))
            {
                foreach (string f in Directory.GetFiles(migLibFolder, "*.xml"))
                {
                    File.Delete(f);
                    homegenie.RaiseEvent(
                        Domains.HomeGenie_System,
                        Domains.HomeGenie_BackupRestore,
                        SourceModule.Master,
                        "HomeGenie Backup Restore",
                        Properties.InstallProgressMessage,
                        "= Removed: MIG Data File '" + f + "'"
                    );
                }
            }
            // Restore MIG configuration/data files if present (from backup folder lib/mig/*.xml)
            migLibFolder = Path.Combine(archiveFolder, "lib", "mig");
            if (Directory.Exists(migLibFolder))
            {
                foreach (string f in Directory.GetFiles(migLibFolder, "*.xml"))
                {
                    File.Copy(f, Path.Combine("lib", "mig", Path.GetFileName(f)), true);
                    homegenie.RaiseEvent(
                        Domains.HomeGenie_System,
                        Domains.HomeGenie_BackupRestore,
                        SourceModule.Master,
                        "HomeGenie Backup Restore",
                        Properties.InstallProgressMessage,
                        "= Restored: '" + Path.Combine("lib", "mig", Path.GetFileName(f)) + "'"
                    );
                }
            }
            // Soft-reload system configuration from newely restored files and save config
            homegenie.SoftReload();
            // Restore user-space automation programs
            serializer = new XmlSerializer(typeof(List<ProgramBlock>));
            reader = new StreamReader(Path.Combine(archiveFolder, "programs.xml"));
            var newProgramsData = (List<ProgramBlock>)serializer.Deserialize(reader);
            reader.Close();
            foreach (var program in newProgramsData)
            {
                var currentProgram = homegenie.ProgramManager.Programs.Find(p => p.Address == program.Address);
                program.IsRunning = false;
                // Only restore user space programs
                if (selectedPrograms.Contains("," + program.Address.ToString() + ",") && program.Address >= ProgramManager.USERSPACE_PROGRAMS_START && program.Address < ProgramManager.PACKAGE_PROGRAMS_START)
                {
                    int oldPid = program.Address;
                    if (currentProgram == null)
                    {
                        var newPid = ((currentProgram != null && currentProgram.Address == program.Address) ? homegenie.ProgramManager.GeneratePid() : program.Address);
                        try
                        {
                            File.Copy(Path.Combine(archiveFolder, "programs", program.Address + ".dll"), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "programs", newPid + ".dll"), true);
                        } catch { }
                        program.Address = newPid;
                        homegenie.ProgramManager.ProgramAdd(program);
                        homegenie.RaiseEvent(
                            Domains.HomeGenie_System,
                            Domains.HomeGenie_BackupRestore,
                            SourceModule.Master,
                            "HomeGenie Backup Restore",
                            Properties.InstallProgressMessage,
                            "= Added: Program '" + program.Name + "' (" + program.Address + ")"
                        );
                    }
                    else if (currentProgram != null)
                    {
                        homegenie.ProgramManager.ProgramRemove(currentProgram);
                        try
                        {
                            File.Copy(Path.Combine(archiveFolder, "programs", program.Address + ".dll"), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "programs", program.Address + ".dll"), true);
                        }
                        catch
                        {
                        }
                        homegenie.ProgramManager.ProgramAdd(program);
                        homegenie.RaiseEvent(
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
                        string sourceFolder = Path.Combine(archiveFolder, "programs", "arduino", oldPid.ToString());
                        string arduinoFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "programs", "arduino", program.Address.ToString());
                        if (Directory.Exists(arduinoFolder))
                            Directory.Delete(arduinoFolder, true);
                        Directory.CreateDirectory(arduinoFolder);
                        foreach (string newPath in Directory.GetFiles(sourceFolder))
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
            homegenie.UpdateProgramsDatabase();
            homegenie.RaiseEvent(
                Domains.HomeGenie_System,
                Domains.HomeGenie_BackupRestore,
                SourceModule.Master,
                "HomeGenie Backup Restore",
                Properties.InstallProgressMessage,
                "= Status: Backup Restore " + (success ? "Succesful" : "Errors")
            );
            homegenie.SaveData();

            return success;
        }

        // Backward compatibility method for HG < 1.1
        private bool UpdateSystemConfig(string configPath)
        {
            string configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "systemconfig.xml");
            string configText = File.ReadAllText(Path.Combine(configPath, "systemconfig.xml"));
            if (configText.IndexOf("<ServicePort>") > 0)
            {
                configText = configText.Replace("SystemConfiguration", "SystemConfiguration_1_0");
                configText = configText.Replace("HomeGenieConfiguration", "HomeGenieConfiguration_1_0");
                // This is old configuration file from HG < 1.1
                SystemConfiguration_1_0 oldConfig;
                SystemConfiguration newConfig = new SystemConfiguration();
                try
                {
                    // Load old config
                    var serializerOld = new XmlSerializer(typeof(SystemConfiguration_1_0));
                    using (var reader = new StringReader(configText))
                        oldConfig = (SystemConfiguration_1_0)serializerOld.Deserialize(reader);
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
                    var webGateway = new Gateway() { Name = "WebServiceGateway", IsEnabled = true };
                    webGateway.Options = new List<Option>();
                    webGateway.Options.Add(new Option("BaseUrl", "/hg/html"));
                    webGateway.Options.Add(new Option("HomePath", "html"));
                    webGateway.Options.Add(new Option("Host", oldConfig.HomeGenie.ServiceHost));
                    webGateway.Options.Add(new Option("Port", oldConfig.HomeGenie.ServicePort.ToString()));
                    webGateway.Options.Add(new Option("Username", "admin"));
                    webGateway.Options.Add(new Option("Password", oldConfig.HomeGenie.UserPassword));
                    webGateway.Options.Add(new Option("HttpCacheIgnore.1", "^.*\\/pages\\/control\\/widgets\\/.*\\.(js|html)$"));
                    webGateway.Options.Add(new Option("HttpCacheIgnore.2", "^.*\\/html\\/index.html"));
                    webGateway.Options.Add(new Option("UrlAlias.1", "api/HomeAutomation.HomeGenie/Logging/RealTime.EventStream:events"));
                    webGateway.Options.Add(new Option("UrlAlias.2", "hg/html/pages/control/widgets/homegenie/generic/images/socket_on.png:hg/html/pages/control/widgets/homegenie/generic/images/switch_on.png"));
                    webGateway.Options.Add(new Option("UrlAlias.3", "hg/html/pages/control/widgets/homegenie/generic/images/socket_off.png:hg/html/pages/control/widgets/homegenie/generic/images/switch_off.png"));
                    webGateway.Options.Add(new Option("UrlAlias.4", "hg/html/pages/control/widgets/homegenie/generic/images/siren.png:hg/html/pages/control/widgets/homegenie/generic/images/siren_on.png"));
                    // TODO: EnableFileCaching value should be read from oldConfig.MIGService.EnableWebCache
                    webGateway.Options.Add(new Option("EnableFileCaching", "false"));
                    newConfig.MigService.Gateways.Add(webGateway);
                    newConfig.MigService.Interfaces = oldConfig.MIGService.Interfaces;
                    foreach(var iface in newConfig.MigService.Interfaces)
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
                    System.Xml.XmlWriterSettings ws = new System.Xml.XmlWriterSettings();
                    ws.Indent = true;
                    ws.Encoding = Encoding.UTF8;
                    System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(newConfig.GetType());
                    System.Xml.XmlWriter wri = System.Xml.XmlWriter.Create(configFile, ws);
                    x.Serialize(wri, newConfig);
                    wri.Close();
                }
                catch (Exception e)
                {
                    MigService.Log.Error(e);
                    return false;
                }
            }
            else
            {
                // HG >= 1.1
                File.Copy(Path.Combine(configPath, "systemconfig.xml"), configFile, true);
            }
            return true;
        }

    }
}

