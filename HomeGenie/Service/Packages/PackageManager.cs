using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Xml.Serialization;
using HomeGenie.Automation;
using HomeGenie.Data;
using HomeGenie.Service.Constants;
using HomeGenie.Utils;
using MIG.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HomeGenie.Service.Packages
{
    public class PackageManager
    {
        private readonly HomeGenieService _homegenie;
        private readonly string _widgetBasePath;

        public PackageManager(HomeGenieService hg)
        {
            _homegenie = hg;
            _widgetBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "html", "pages", "control", "widgets");
        }

        public bool InstallPackage(string packageSourceUrl)
        {
            PackageDefinition pkgData;
            bool success;

            (success, pkgData) = DownloadPackageDefinition(packageSourceUrl);

            // Download and install package files
            if (success && pkgData != null)
            {
                // Import Automation Programs in package
                foreach (var program in pkgData.Programs)
                {
                    success = DownloadAndInstallProgram(packageSourceUrl, program);
                }

                // Import Widgets in package
                foreach (var widget in pkgData.Widgets)
                {
                    success = DownloadAndInstallWidget(packageSourceUrl, widget);
                }

                // Import MIG Interfaces in package
                foreach (var @interface in pkgData.Interfaces)
                {
                    success = DownloadAndInstallInterface(packageSourceUrl, @interface);
                }
            }
            else
            {
                success = false;
            }

            if (success)
            {
                pkgData.SourceUrl = packageSourceUrl;
                pkgData.InstallDate = DateTime.UtcNow;
                AddInstalledPackage(pkgData);
                _homegenie.RaiseEvent(
                    Domains.HomeGenie_System,
                    Domains.HomeGenie_PackageInstaller,
                    SourceModule.Master,
                    "HomeGenie Package Installer",
                    Properties.InstallProgressMessage,
                    "= Status: Package Install Successful"
                );
            }
            else
            {
                _homegenie.RaiseEvent(
                    Domains.HomeGenie_System,
                    Domains.HomeGenie_PackageInstaller,
                    SourceModule.Master,
                    "HomeGenie Package Installer",
                    Properties.InstallProgressMessage,
                    "= Status: Package Install Error"
                );
            }

            return success;
        }

        private (bool, PackageDefinition) DownloadPackageDefinition(string packageSourceUrl)
        {
            var success = false;
            dynamic pkgData = null;

            // Download package specs
            _homegenie.RaiseEvent(
                Domains.HomeGenie_System,
                Domains.HomeGenie_PackageInstaller,
                SourceModule.Master,
                "HomeGenie Package Installer",
                Properties.InstallProgressMessage,
                "= Downloading: package.json"
            );

            using (var client = new WebClient())
            {
                try
                {
                    var pkgJson = client.DownloadString(packageSourceUrl + "/package.json");
                    pkgData = JsonConvert.DeserializeObject<PackageDefinition>(pkgJson);
                    success = true;
                }
                catch (Exception e)
                {
                    _homegenie.RaiseEvent(
                        Domains.HomeGenie_System,
                        Domains.HomeGenie_PackageInstaller,
                        SourceModule.Master,
                        "HomeGenie Package Installer",
                        Properties.InstallProgressMessage,
                        "= ERROR: '" + e.Message + "'"
                    );
                }

                client.Dispose();
            }

            return (success, pkgData);
        }

        private bool DownloadAndInstallProgram(string packageSourceUrl, PackageProgramDefinition program)
        {
            var success = false;
            _homegenie.RaiseEvent(
                Domains.HomeGenie_System,
                Domains.HomeGenie_PackageInstaller,
                SourceModule.Master,
                "HomeGenie Package Installer",
                Properties.InstallProgressMessage,
                "= Downloading: " + program.File
            );

            var tempFolder = Utility.GetTmpFolder();
            Utility.FolderCleanUp(tempFolder);
            var programFile = Path.Combine(tempFolder, program.File);

            using (var client = new WebClient())
            {
                try
                {
                    client.DownloadFile(packageSourceUrl + "/" + program.File, programFile);
                    success = true;
                }
                catch (Exception e)
                {
                    _homegenie.RaiseEvent(
                        Domains.HomeGenie_System,
                        Domains.HomeGenie_PackageInstaller,
                        SourceModule.Master,
                        "HomeGenie Package Installer",
                        Properties.InstallProgressMessage,
                        "= ERROR: '" + e.Message + "'"
                    );
                }

                client.Dispose();
            }

            if (!success)
                return false;

            _homegenie.RaiseEvent(
                Domains.HomeGenie_System,
                Domains.HomeGenie_PackageInstaller,
                SourceModule.Master,
                "HomeGenie Package Installer",
                Properties.InstallProgressMessage,
                "= Installing: " + program.Name
            );

            var pid = int.Parse(program.Uid);
            var enabled = true; // by default enable package programs after installing them
            var oldProgram = _homegenie.ProgramManager.ProgramGet(pid);
            if (oldProgram != null)
            {
                _homegenie.RaiseEvent(
                    Domains.HomeGenie_System,
                    Domains.HomeGenie_PackageInstaller,
                    SourceModule.Master,
                    "HomeGenie Package Installer",
                    Properties.InstallProgressMessage,
                    "= Replacing: '" + oldProgram.Name + "' with pid " + pid
                );
                enabled = oldProgram.IsEnabled; // if the program was already installed, inherit IsEnabled
                _homegenie.ProgramManager.ProgramRemove(oldProgram);
            }

            var programBlock = ProgramImport(pid, programFile, program.Group);
            if (programBlock != null)
            {
                var groupName = programBlock.Group;
                if (!string.IsNullOrWhiteSpace(groupName))
                {
                    // Add automation program group if does not exist
                    var newGroup = new Group {Name = groupName};
                    if (_homegenie.AutomationGroups.Find(g => g.Name == newGroup.Name) == null)
                    {
                        _homegenie.AutomationGroups.Add(newGroup);
                        _homegenie.UpdateAutomationGroupsDatabase();
                    }
                }

                programBlock.IsEnabled = enabled;
                _homegenie.RaiseEvent(
                    Domains.HomeGenie_System,
                    Domains.HomeGenie_PackageInstaller,
                    SourceModule.Master,
                    "HomeGenie Package Installer",
                    Properties.InstallProgressMessage,
                    "= Installed: '" + program.Name + "' as pid " + pid
                );
            }
            else
            {
                // TODO: report error and stop the package install procedure
                success = false;
            }

            return success;
        }

        private bool DownloadAndInstallWidget(string packageSourceUrl, PackageInterfaceDefinition widget)
        {
            var success = false;
            _homegenie.RaiseEvent(
                Domains.HomeGenie_System,
                Domains.HomeGenie_PackageInstaller,
                SourceModule.Master,
                "HomeGenie Package Installer",
                Properties.InstallProgressMessage,
                "= Downloading: " + widget.File
            );

            var tempFolder = Utility.GetTmpFolder();
            Utility.FolderCleanUp(tempFolder);
            var widgetFile = Path.Combine(tempFolder, widget.File);

            using (var client = new WebClient())
            {
                try
                {
                    client.DownloadFile(packageSourceUrl + "/" + widget.File, widgetFile);
                    success = true;
                }
                catch (Exception e)
                {
                    _homegenie.RaiseEvent(
                        Domains.HomeGenie_System,
                        Domains.HomeGenie_PackageInstaller,
                        SourceModule.Master,
                        "HomeGenie Package Installer",
                        Properties.InstallProgressMessage,
                        "= ERROR: '" + e.Message + "'"
                    );
                }

                client.Dispose();
            }

            if (success && WidgetImport(widgetFile, tempFolder))
            {
                _homegenie.RaiseEvent(
                    Domains.HomeGenie_System,
                    Domains.HomeGenie_PackageInstaller,
                    SourceModule.Master,
                    "HomeGenie Package Installer",
                    Properties.InstallProgressMessage,
                    "= Installed: '" + widget.Name + "'"
                );
            }
            else
            {
                // TODO: report error and stop the package install procedure
                success = false;
            }

            return success;
        }

        private bool DownloadAndInstallInterface(string packageSourceUrl, PackageInterfaceDefinition @interface)
        {
            var success = false;
            _homegenie.RaiseEvent(
                Domains.HomeGenie_System,
                Domains.HomeGenie_PackageInstaller,
                SourceModule.Master,
                "HomeGenie Package Installer",
                Properties.InstallProgressMessage,
                "= Downloading: " + @interface.File
            );

            var tempFolder = Utility.GetTmpFolder();
            Utility.FolderCleanUp(tempFolder);
            var migInterfaceFile = Path.Combine(tempFolder, @interface.File);

            using (var client = new WebClient())
            {
                try
                {
                    client.DownloadFile(packageSourceUrl + "/" + @interface.File, migInterfaceFile);
                    ArchiveHelper.UncompressZip(migInterfaceFile, tempFolder);
                    File.Delete(migInterfaceFile);
                    success = true;
                }
                catch (Exception e)
                {
                    _homegenie.RaiseEvent(
                        Domains.HomeGenie_System,
                        Domains.HomeGenie_PackageInstaller,
                        SourceModule.Master,
                        "HomeGenie Package Installer",
                        Properties.InstallProgressMessage,
                        "= ERROR: '" + e.Message + "'"
                    );
                }

                client.Dispose();
            }

            if (success && InterfaceInstall(tempFolder))
            {
                _homegenie.RaiseEvent(
                    Domains.HomeGenie_System,
                    Domains.HomeGenie_PackageInstaller,
                    SourceModule.Master,
                    "HomeGenie Package Installer",
                    Properties.InstallProgressMessage,
                    "= Installed: '" + @interface.Name + "'"
                );
            }
            else
            {
                // TODO: report error and stop the package install procedure
                success = false;
            }

            return success;
        }

        /// <summary>
        /// Searches for an installed package with given SourceUrl
        /// </summary>
        /// <param name="packageSourceUrl">Package SourceUrl</param>
        /// <returns>Package definition if package is found, null otherwise</returns>
        public PackageDefinition GetInstalledPackage(string packageSourceUrl)
        {
            var pkgList = LoadInstalledPackages();
            return pkgList.Find(p => p.SourceUrl == packageSourceUrl);
        }

        private void AddInstalledPackage(PackageDefinition pkgObject)
        {
            var pkgList = LoadInstalledPackages();
            pkgList.RemoveAll(p => p.SourceUrl == pkgObject.SourceUrl);
            pkgList.Add(pkgObject);
            File.WriteAllText(FilePaths.InstalledPackagesFilePath, JsonConvert.SerializeObject(pkgList, Formatting.Indented));
        }

        public List<PackageDefinition> LoadInstalledPackages()
        {
            var pkgList = new List<PackageDefinition>();
            if (!File.Exists(FilePaths.InstalledPackagesFilePath))
                return pkgList;

            try
            {
                pkgList = JsonConvert.DeserializeObject<List<PackageDefinition>>(File.ReadAllText(FilePaths.InstalledPackagesFilePath));
            }
            catch (Exception e)
            {
                // TODO: report exception
            }

            return pkgList;
        }

        public Interface GetInterfaceConfig(string configFile)
        {
            Interface iface = null;
            using (var ifaceReader = new StreamReader(configFile))
            {
                var ifaceSerializer = new XmlSerializer(typeof(Interface));
                iface = (Interface) ifaceSerializer.Deserialize(ifaceReader);
                ifaceReader.Close();
            }

            return iface;
        }

        private void AddWidgetMapping(string jsonMap)
        {
            /*
               // example widget mapping
               [
                  {
                      Description     : "Z-Wave.Me Floor Thermostat",
                      Widget          : "Bounz/Z-Wave.Me/thermostat",
                      MatchProperty   : "ZWaveNode.ManufacturerSpecific",
                      MatchValue      : "0115:0024:0001"
                  }
               ]
            */
            var mapConfigFile = "html/pages/control/widgets/configuration.json";
            var mapList = JArray.Parse(File.ReadAllText(mapConfigFile)).ToObject<List<dynamic>>();
            var widgetMap = JArray.Parse(jsonMap).ToObject<List<dynamic>>();
            try
            {
                foreach (var map in widgetMap)
                {
                    mapList.RemoveAll(m => m.MatchProperty.ToString() == map.MatchProperty.ToString() && m.MatchValue.ToString() == map.MatchValue.ToString());
                    mapList.Add(map);
                }

                File.WriteAllText(mapConfigFile, JsonConvert.SerializeObject(mapList, Formatting.Indented));
            }
            catch
            {
                // TODO: report exception
            }
        }

        public bool WidgetImport(string archiveFile, string importPath)
        {
            const string widgetInfoFile = "widget.info";
            var success = false;
            var extractedFiles = ArchiveHelper.UncompressZip(archiveFile, importPath);
            if (File.Exists(Path.Combine(importPath, widgetInfoFile)))
            {
                // Read "widget.info" and, if a mapping is present, add it to "html/pages/control/widgets/configuration.json"
                var mapping = File.ReadAllText(Path.Combine(importPath, widgetInfoFile));
                if (mapping.StartsWith("["))
                    AddWidgetMapping(mapping);
                foreach (var f in extractedFiles)
                {
                    // copy only files contained in sub-folders, avoid copying zip-root files
                    if (Path.GetDirectoryName(f) != "")
                    {
                        var destFolder = Path.Combine(_widgetBasePath, Path.GetDirectoryName(f));
                        if (!Directory.Exists(destFolder))
                            Directory.CreateDirectory(destFolder);
                        // TODO HGBE-10 Move custom widgets to data folder and correct JS and C# code related to widgets
                        File.Copy(Path.Combine(importPath, f), Path.Combine(_widgetBasePath, f), true);
                    }
                }

                success = true;
            }

            return success;
        }

        public ProgramBlock ProgramImport(int newPid, string archiveName, string groupName)
        {
            var reader = new StreamReader(archiveName);
            var signature = new char[2];
            reader.Read(signature, 0, 2);
            reader.Close();
            if (signature[0] == 'P' && signature[1] == 'K')
            {
                // Read and uncompress zip file content (arduino program bundle)
                var zipFileName = archiveName.Replace(".hgx", ".zip");
                if (File.Exists(zipFileName))
                    File.Delete(zipFileName);
                File.Move(archiveName, zipFileName);
                var destFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Utility.GetTmpFolder(), "import");
                if (Directory.Exists(destFolder))
                    Directory.Delete(destFolder, true);
                ArchiveHelper.UncompressZip(zipFileName, destFolder);
                var bundleFolder = Path.Combine(FilePaths.ProgramsFolder, "arduino", newPid.ToString());
                if (Directory.Exists(bundleFolder))
                    Directory.Delete(bundleFolder, true);
                if (!Directory.Exists(Path.Combine(FilePaths.ProgramsFolder, "arduino")))
                    Directory.CreateDirectory(Path.Combine(FilePaths.ProgramsFolder, "arduino"));
                Directory.Move(Path.Combine(destFolder, "src"), bundleFolder);
                reader = new StreamReader(Path.Combine(destFolder, "program.hgx"));
            }
            else
            {
                reader = new StreamReader(archiveName);
            }

            var serializer = new XmlSerializer(typeof(ProgramBlock));
            var newProgram = (ProgramBlock) serializer.Deserialize(reader);
            reader.Close();

            newProgram.Address = newPid;
            newProgram.Group = groupName;
            _homegenie.ProgramManager.ProgramAdd(newProgram);

            newProgram.IsEnabled = false;
            newProgram.ScriptErrors = "";
            newProgram.Engine.SetHost(_homegenie);

            if (newProgram.Type.ToLower() != "arduino")
            {
                _homegenie.ProgramManager.CompileScript(newProgram);
            }

            return newProgram;
        }

        public bool InterfaceInstall(string sourceFolder)
        {
            // install the interface package
            var configFile = Path.Combine(sourceFolder, "configuration.xml");
            if (!File.Exists(configFile))
                return false;

            var iface = GetInterfaceConfig(configFile);
            if (iface == null)
                return false;

            File.Delete(configFile);

            // TODO: !IMPORTANT!
            // TODO: since AppDomains are not implemented in MIG, a RESTART is required to load the new Assembly
            // TODO: HG should ask for RESTART in the UI
            _homegenie.MigService.RemoveInterface(iface.Domain);

            var configletName = iface.Domain.Substring(iface.Domain.LastIndexOf(".") + 1).ToLower();
            // TODO HGBE-10 place configlets beside main interface files and refactor JS and C# code to look for configlets there
            var configletPath = Path.Combine("html", "pages", "configure", "interfaces", "configlet", configletName + ".html");
            File.Copy(Path.Combine(sourceFolder, "configlet.html"), configletPath, true);

            // TODO HGBE-10 place interface image beside main interface files and refactor JS and C# code to look for configlets there
            var logoPath = Path.Combine("html", "images", "interfaces", configletName + ".png");
            File.Copy(Path.Combine(sourceFolder, "logo.png"), logoPath, true);

            // copy other interface files to mig folder (dll and dependencies)
            var interfaceInstallationFolder = Path.Combine(FilePaths.InterfacesFolder, iface.Domain);
            if (!Directory.Exists(interfaceInstallationFolder))
                Directory.CreateDirectory(interfaceInstallationFolder);
            var filesToCopy = new DirectoryInfo(sourceFolder).GetFiles();
            foreach (var file in filesToCopy)
            {
                var destFile = Path.Combine(interfaceInstallationFolder, Path.GetFileName(file.Name));
                File.Copy(file.FullName, destFile, true);
            }

            _homegenie.SystemConfiguration.MigService.Interfaces.RemoveAll(i => i.Domain == iface.Domain);
            _homegenie.SystemConfiguration.MigService.Interfaces.Add(iface);
            _homegenie.SystemConfiguration.Update();
            _homegenie.MigService.AddInterface(iface.Domain, iface.AssemblyName);

            return true;
        }
    }
}
