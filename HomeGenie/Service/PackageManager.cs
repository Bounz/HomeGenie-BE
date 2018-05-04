using System;
using System.IO;
using System.Net;
using System.Xml.Serialization;
using System.Collections.Generic;
using HomeGenie.Automation;
using HomeGenie.Data;
using HomeGenie.Service.Constants;

using MIG.Config;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HomeGenie.Service
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

        public bool InstallPackage(string pkgFolderUrl, string tempFolderPath)
        {
            var installFolder = Path.Combine(tempFolderPath, "pkg");
            dynamic pkgData = null;
            var success = true;
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
                    var pkgJson = "[" + client.DownloadString(pkgFolderUrl + "/package.json") + "]";
                    pkgData = (JsonConvert.DeserializeObject(pkgJson) as JArray)[0];
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
                    success = false;
                }
                client.Dispose();
            }

            // Download and install package files
            if (success && pkgData != null)
            {
                // Import Automation Programs in package
                foreach (var program in pkgData.programs)
                {
                    _homegenie.RaiseEvent(
                        Domains.HomeGenie_System,
                        Domains.HomeGenie_PackageInstaller,
                        SourceModule.Master,
                        "HomeGenie Package Installer",
                        Properties.InstallProgressMessage,
                        "= Downloading: " + program.file.ToString()
                    );
                    Utility.FolderCleanUp(installFolder);
                    string programFile = Path.Combine(installFolder, program.file.ToString());
                    if (File.Exists(programFile))
                        File.Delete(programFile);
                    using (var client = new WebClient())
                    {
                        try
                        {
                            client.DownloadFile(pkgFolderUrl + "/" + program.file.ToString(), programFile);
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
                            success = false;
                        }
                        client.Dispose();
                    }
                    if (success)
                    {
                        _homegenie.RaiseEvent(
                            Domains.HomeGenie_System,
                            Domains.HomeGenie_PackageInstaller,
                            SourceModule.Master,
                            "HomeGenie Package Installer",
                            Properties.InstallProgressMessage,
                            "= Installing: " + program.name.ToString()
                        );
                        int pid = int.Parse(program.uid.ToString());
                        // by default enable package programs after installing them
                        var enabled = true;
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
                            // if the program was already installed, inherit IsEnabled
                            enabled = oldProgram.IsEnabled;
                            _homegenie.ProgramManager.ProgramRemove(oldProgram);
                        }
                        var programBlock = ProgramImport(pid, programFile, program.group.ToString());
                        if (programBlock != null)
                        {
                            string groupName = programBlock.Group;
                            if (!String.IsNullOrWhiteSpace(groupName))
                            {
                                // Add automation program group if does not exist
                                var newGroup = new Group() { Name = groupName };
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
                                "= Installed: '" + program.name.ToString() + "' as pid " + pid
                            );
                        }
                        else
                        {
                            // TODO: report error and stop the package install procedure
                            success = false;
                        }
                    }
                }

                // Import Widgets in package
                foreach (var widget in pkgData.widgets)
                {
                    _homegenie.RaiseEvent(
                        Domains.HomeGenie_System,
                        Domains.HomeGenie_PackageInstaller,
                        SourceModule.Master,
                        "HomeGenie Package Installer",
                        Properties.InstallProgressMessage,
                        "= Downloading: " + widget.file.ToString()
                    );
                    Utility.FolderCleanUp(installFolder);
                    string widgetFile = Path.Combine(installFolder, widget.file.ToString());
                    if (File.Exists(widgetFile))
                        File.Delete(widgetFile);
                    using (var client = new WebClient())
                    {
                        try
                        {
                            client.DownloadFile(pkgFolderUrl + "/" + widget.file.ToString(), widgetFile);
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
                            success = false;
                        }
                        client.Dispose();
                    }
                    if (success && WidgetImport(widgetFile, installFolder))
                    {
                        _homegenie.RaiseEvent(
                            Domains.HomeGenie_System,
                            Domains.HomeGenie_PackageInstaller,
                            SourceModule.Master,
                            "HomeGenie Package Installer",
                            Properties.InstallProgressMessage,
                            "= Installed: '" + widget.name.ToString() + "'"
                        );
                    }
                    else
                    {
                        // TODO: report error and stop the package install procedure
                        success = false;
                    }
                }

                // Import MIG Interfaces in package
                foreach (var migface in pkgData.interfaces)
                {
                    _homegenie.RaiseEvent(
                        Domains.HomeGenie_System,
                        Domains.HomeGenie_PackageInstaller,
                        SourceModule.Master,
                        "HomeGenie Package Installer",
                        Properties.InstallProgressMessage,
                        "= Downloading: " + migface.file.ToString()
                    );
                    Utility.FolderCleanUp(installFolder);
                    string migfaceFile = Path.Combine(installFolder, migface.file.ToString());
                    if (File.Exists(migfaceFile))
                        File.Delete(migfaceFile);
                    using (var client = new WebClient())
                    {
                        try
                        {
                            client.DownloadFile(pkgFolderUrl + "/" + migface.file.ToString(), migfaceFile);
                            Utility.UncompressZip(migfaceFile, installFolder);
                            File.Delete(migfaceFile);
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
                            success = false;
                        }
                        client.Dispose();
                    }
                    if (success && InterfaceInstall(installFolder))
                    {
                        _homegenie.RaiseEvent(
                            Domains.HomeGenie_System,
                            Domains.HomeGenie_PackageInstaller,
                            SourceModule.Master,
                            "HomeGenie Package Installer",
                            Properties.InstallProgressMessage,
                            "= Installed: '" + migface.name.ToString() + "'"
                        );
                    }
                    else
                    {
                        // TODO: report error and stop the package install procedure
                        success = false;
                    }
                }
            }
            else
            {
                success = false;
            }
            if (success)
            {
                pkgData.folder_url = pkgFolderUrl;
                pkgData.install_date = DateTime.UtcNow;
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

        public dynamic GetInstalledPackage(string pkgFolderUrl)
        {
            var pkgList = LoadInstalledPackages();
            return pkgList.Find(p => p.folder_url.ToString() == pkgFolderUrl);
        }

        public void AddInstalledPackage(dynamic pkgObject)
        {
            var pkgList = LoadInstalledPackages();
            pkgList.RemoveAll(p => p.folder_url.ToString() == pkgObject.folder_url.ToString());
            pkgList.Add(pkgObject);
            File.WriteAllText(FilePaths.InstalledPackagesFilePath, JsonConvert.SerializeObject(pkgList, Formatting.Indented));
        }

        public List<dynamic> LoadInstalledPackages()
        {
            var pkgList = new List<dynamic>();
            if (File.Exists(FilePaths.InstalledPackagesFilePath))
            {
                try
                {
                    pkgList = JArray.Parse(File.ReadAllText(FilePaths.InstalledPackagesFilePath)).ToObject<List<dynamic>>();
                }
                catch (Exception e)
                {
                    // TODO: report exception
                }
            }
            return pkgList;
        }

        public Interface GetInterfaceConfig(string configFile)
        {
            Interface iface = null;
            using (var ifaceReader = new StreamReader(configFile))
            {
                var ifaceSerializer = new XmlSerializer(typeof(Interface));
                iface = (Interface)ifaceSerializer.Deserialize(ifaceReader);
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
            var success = false;
            var widgetInfoFile = "widget.info";
            var extractedFiles = Utility.UncompressZip(archiveFile, importPath);
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
                        File.Copy(Path.Combine(importPath, f), Path.Combine(_widgetBasePath, f), true);
                    }
                }
                success = true;
            }
            return success;
        }

        public ProgramBlock ProgramImport(int newPid, string archiveName, string groupName)
        {
            ProgramBlock newProgram;
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
                Utility.UncompressZip(zipFileName, destFolder);
                var bundleFolder = Path.Combine("programs", "arduino", newPid.ToString());
                if (Directory.Exists(bundleFolder))
                    Directory.Delete(bundleFolder, true);
                if (!Directory.Exists(Path.Combine("programs", "arduino")))
                    Directory.CreateDirectory(Path.Combine("programs", "arduino"));
                Directory.Move(Path.Combine(destFolder, "src"), bundleFolder);
                reader = new StreamReader(Path.Combine(destFolder, "program.hgx"));
            }
            else
            {
                reader = new StreamReader(archiveName);
            }
            var serializer = new XmlSerializer(typeof(ProgramBlock));
            newProgram = (ProgramBlock)serializer.Deserialize(reader);
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
            var success = false;
            // install the interface package
            var configFile = Path.Combine(sourceFolder, "configuration.xml");
            var iface = GetInterfaceConfig(configFile);
            if (iface != null)
            {
                File.Delete(configFile);

                // TODO: !IMPORTANT!
                // TODO: since AppDomains are not implemented in MIG, a RESTART is required to load the new Assembly
                // TODO: HG should ask for RESTART in the UI
                _homegenie.MigService.RemoveInterface(iface.Domain);

                var configletName = iface.Domain.Substring(iface.Domain.LastIndexOf(".") + 1).ToLower();
                var configletPath = Path.Combine("html", "pages", "configure", "interfaces", "configlet", configletName + ".html");
                if (File.Exists(configletPath))
                {
                    File.Delete(configletPath);
                }
                File.Move(Path.Combine(sourceFolder, "configlet.html"), configletPath);

                var logoPath = Path.Combine("html", "images", "interfaces", configletName + ".png");
                if (File.Exists(logoPath))
                {
                    File.Delete(logoPath);
                }
                File.Move(Path.Combine(sourceFolder, "logo.png"), logoPath);

                // copy other interface files to mig folder (dll and dependencies)
                var migFolder = Path.Combine("lib", "mig");
                var dir = new DirectoryInfo(sourceFolder);
                foreach (var f in dir.GetFiles())
                {
                    var destFile = Path.Combine(migFolder, Path.GetFileName(f.FullName));
                    if (File.Exists(destFile))
                    {
                        try
                        {
                            File.Delete(destFile + ".old");
                        }
                        catch { }
                        try
                        {
                            File.Move(destFile, destFile + ".old");
                            File.Delete(destFile + ".old");
                        }
                        catch { }
                    }
                    File.Move(f.FullName, destFile);
                }

                _homegenie.SystemConfiguration.MigService.Interfaces.RemoveAll(i => i.Domain == iface.Domain);
                _homegenie.SystemConfiguration.MigService.Interfaces.Add(iface);
                _homegenie.SystemConfiguration.Update();
                _homegenie.MigService.AddInterface(iface.Domain, iface.AssemblyName);

                success = true;
            }
            return success;
        }
    }
}
