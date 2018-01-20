using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Xml.Serialization;
using HomeGenie.Automation;
using HomeGenie.Automation.Scheduler;
using HomeGenie.Data;
using Newtonsoft.Json;

namespace HomeGenie.Service.Updates
{
    public class UpdateInstaller
    {
        public delegate void ArchiveDownloadEvent(object sender, ArchiveDownloadEventArgs args);
        public ArchiveDownloadEvent ArchiveDownloadUpdate;
        
        public delegate void InstallProgressMessageEvent(object sender, string message);
        public InstallProgressMessageEvent InstallProgressMessage;

        private static string UpdateFolder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_update");
        private static string UpdateBaseFolder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_update", "files");

        private readonly HomeGenieService _homegenieService;

        public UpdateInstaller(HomeGenieService homegenieService)
        {
            _homegenieService = homegenieService;
        }

        private bool AreFilesIdentical(string sourceFile, string destinationFile)
        {
            using (var md5 = MD5.Create())
            {
                try
                {
                    // Try getting files' hash
                    string localHash;
                    using (var stream = File.OpenRead(destinationFile))
                    {
                        localHash = BitConverter.ToString(md5.ComputeHash(stream));
                    }

                    var remoteHash = "";
                    using (var stream = File.OpenRead(sourceFile))
                    {
                        remoteHash = BitConverter.ToString(md5.ComputeHash(stream));
                    }
                    if (localHash != remoteHash)
                    {
                        return true;
                        //Console.WriteLine("CHANGED {0}", destinationFile);
                        //Console.WriteLine("   - LOCAL  {0}", localHash);
                        //Console.WriteLine("   - REMOTE {0}", remoteHash);
                    }
                }
                catch (Exception e)
                {
                    // this mostly happen if the destinationFile is un use and cannot be opened,
                    // file is then ignored if hash cannot be calculated
                }
            }

            return false;
        }

        public InstallStatus InstallFiles()
        {
            var status = InstallStatus.Success;
            var restartRequired = false;
            var oldFilesPath = Path.Combine(UpdateFolder, "oldfiles");
            var newFilesPath = Path.Combine(UpdateFolder, "HomeGenie_update");
            if (Directory.Exists(UpdateBaseFolder))
            {
                Directory.Move(UpdateBaseFolder, newFilesPath);
            }
            Utility.FolderCleanUp(oldFilesPath);

            if (!Directory.Exists(newFilesPath))
                return status;

            LogMessage("= Copying new files...");
            foreach (var file in Directory.EnumerateFiles(newFilesPath, "*", SearchOption.AllDirectories))
            {
                var doNotCopy = false;

                var destinationFolder = Path.GetDirectoryName(file).Replace(newFilesPath, "").TrimStart('/').TrimStart('\\');
                var destinationFile = Path.Combine(destinationFolder, Path.GetFileName(file)).TrimStart(Directory.GetDirectoryRoot(Directory.GetCurrentDirectory()).ToArray()).TrimStart('/').TrimStart('\\');

                // Update file only if different from local one
                bool processFile;
                if (File.Exists(destinationFile))
                {
                    processFile = AreFilesIdentical(file, destinationFile);
                }
                else
                {
                    processFile = true;
                }

                if (!processFile)
                    continue;

                // Some files needs to be handled differently than just copying
                if (File.Exists(destinationFile))
                {
                    switch (destinationFile)
                    {
                        case "automationgroups.xml":
                            doNotCopy = true;
                            status = UpdateAutomationGroups(file) ? InstallStatus.Success : InstallStatus.Error;
                            break;
                        case "groups.xml":
                            doNotCopy = true;
                            status = UpdateGroups(file) ? InstallStatus.Success : InstallStatus.Error;
                            break;
                        case "lircconfig.xml":
                        case "modules.xml":
                        case "homegenie_stats.db":
                            doNotCopy = true;
                            break;
                        case "programs.xml":
                            doNotCopy = true;
                            status = UpdatePrograms(file) ? InstallStatus.Success : InstallStatus.Error;
                            break;
                        case "scheduler.xml":
                            doNotCopy = true;
                            status = UpdateScheduler(file) ? InstallStatus.Success : InstallStatus.Error;
                            break;
                        case "systemconfig.xml":
                            doNotCopy = true;
                            status = UpdateSystemConfig(file) ? InstallStatus.Success : InstallStatus.Error;
                            break;
                    }

                    if (status == InstallStatus.Error)
                        break;
                }

                if (doNotCopy)
                    continue;

                // Update the file
                if (destinationFile.EndsWith(".exe") || destinationFile.EndsWith(".dll") || destinationFile.EndsWith(".so"))
                    restartRequired = true;

                if (!string.IsNullOrWhiteSpace(destinationFolder) && !Directory.Exists(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                // backup current file before replacing it
                if (File.Exists(destinationFile))
                {
                    var oldFile = Path.Combine(oldFilesPath, destinationFile);
                    Directory.CreateDirectory(Path.GetDirectoryName(oldFile));

                    LogMessage("+ Backup file '" + oldFile + "'");

                    // TODO: delete oldFilesPath before starting update
                    //File.Delete(oldFile);

                    if (destinationFile.EndsWith(".exe") || destinationFile.EndsWith(".dll"))
                    {
                        // this will allow replace of new exe and dll files
                        File.Move(destinationFile, oldFile);
                    }
                    else
                    {
                        File.Copy(destinationFile, oldFile);
                    }
                }

                try
                {
                    LogMessage("+ Copying file '" + destinationFile + "'");
                    if (!string.IsNullOrWhiteSpace(Path.GetDirectoryName(destinationFile)) && !Directory.Exists(Path.GetDirectoryName(destinationFile)))
                    {
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
                            LogMessage("+ Created folder '" + Path.GetDirectoryName(destinationFile) + "'");
                        }
                        catch
                        {
                        }
                    }
                    File.Copy(file, destinationFile, true);
                }
                catch (Exception e)
                {
                    LogMessage("! Error copying file '" + destinationFile + "' (" + e.Message + ")");
                    status = InstallStatus.Error;
                    break;
                }

            }

            if (status == InstallStatus.Error)
            {
                // TODO: should revert!
                LogMessage("! ERROR update aborted.");
            }
            else if (restartRequired)
            {
                status = InstallStatus.RestartRequired;
            }

            return status;
        }
        
        public bool DownloadUpdateFiles(ReleaseInfo releaseToInstall)
        {
            if (releaseToInstall == null)
                return true;

            if (Directory.Exists(UpdateFolder))
                Directory.Delete(UpdateFolder, true);

            var success = true;

            var files = DownloadAndUncompress(releaseToInstall);
            if (files == null) // || files.Count == 0)
            {
                success = false;
            }

            return success;
        }

        private List<string> DownloadAndUncompress(ReleaseInfo releaseInfo)
        {
            if (ArchiveDownloadUpdate != null)
                ArchiveDownloadUpdate(this, new ArchiveDownloadEventArgs(releaseInfo, ArchiveDownloadStatus.Downloading));
            //
            var destinationFolder = Path.Combine(UpdateFolder, "files");
            var archiveName = Path.Combine(UpdateFolder, "archives", "hg_update_" + releaseInfo.Version.Replace(" ", "_").Replace(".", "_") + ".zip");
            if (!Directory.Exists(Path.GetDirectoryName(archiveName)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(archiveName));
            }
            using (var client = new WebClient())
            {
                client.Headers.Add("user-agent", "HomeGenieUpdater/1.0 (compatible; MSIE 7.0; Windows NT 6.0)");
                try
                {
                    client.DownloadFile(releaseInfo.DownloadUrl, archiveName);
                }
                catch (Exception)
                {
                    if (ArchiveDownloadUpdate != null)
                        ArchiveDownloadUpdate(this, new ArchiveDownloadEventArgs(releaseInfo, ArchiveDownloadStatus.Error));
                    return null;
                    //                throw;
                }
                finally
                {
                    client.Dispose();
                }
            }

            // Unarchive (unzip)
            ArchiveDownloadUpdate?.Invoke(this, new ArchiveDownloadEventArgs(releaseInfo, ArchiveDownloadStatus.Decompressing));

            var errorOccurred = false;
            var files = Utility.UncompressTgz(archiveName, destinationFolder);
            errorOccurred = files.Count == 0;

            ArchiveDownloadUpdate?.Invoke(this,
                errorOccurred
                    ? new ArchiveDownloadEventArgs(releaseInfo, ArchiveDownloadStatus.Error)
                    : new ArchiveDownloadEventArgs(releaseInfo, ArchiveDownloadStatus.Completed));

            // update release_info.xml file with last releaseInfo ReleaseDate field in order to reflect github release date
            if (files.Contains(UpdatesHelper.ReleaseFile))
            {
                var ri = UpdatesHelper.GetReleaseInfoFromFile(Path.Combine(destinationFolder, UpdatesHelper.ReleaseFile));
                ri.ReleaseDate = releaseInfo.ReleaseDate.ToUniversalTime();
                var serializer = new XmlSerializer(typeof(ReleaseInfo));
                using (TextWriter writer = new StreamWriter(Path.Combine(destinationFolder, UpdatesHelper.ReleaseFile)))
                {
                    serializer.Serialize(writer, ri);
                }
            }

            return files;
        }
        
        private bool UpdatePrograms(string file)
        {
            var success = true;
            try
            {
                var serializer = new XmlSerializer(typeof(List<ProgramBlock>));
                var reader = new StreamReader(file);
                var newProgramList = (List<ProgramBlock>)serializer.Deserialize(reader);
                reader.Close();
                //
                if (newProgramList.Count > 0)
                {
                    var configChanged = false;
                    foreach (var program in newProgramList)
                    {

                        // Only system programs are to be updated
                        if (program.Address < ProgramManager.USERSPACE_PROGRAMS_START)
                        {
                            var oldProgram = _homegenieService.ProgramManager.Programs.Find(p => p.Address == program.Address);
                            if (oldProgram != null)
                            {

                                // Check new program against old one to find out if they differ
                                var changed = ProgramsDiff(oldProgram, program);
                                if (!changed)
                                    continue;

                                // Preserve IsEnabled status if program already exist
                                program.IsEnabled = oldProgram.IsEnabled;
                                LogMessage("* Updating Automation Program: " + program.Name + " (" + program.Address + ")");
                                _homegenieService.ProgramManager.ProgramRemove(oldProgram);

                            }
                            else
                            {
                                LogMessage("+ Adding Automation Program: " + program.Name + " (" + program.Address + ")");
                            }

                            // Try copying the new program files (binary dll or arduino sketch files)
                            try
                            {
                                if (program.Type.ToLower() == "csharp")
                                {
                                    File.Copy(Path.Combine(UpdateBaseFolder, "programs", program.Address + ".dll"), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "programs", program.Address + ".dll"), true);
                                }
                                else if (program.Type.ToLower() == "arduino")
                                {
                                    // copy arduino project files...
                                    // TODO: this is untested yet
                                    var sourceFolder = Path.Combine(UpdateBaseFolder, "programs", "arduino", program.Address.ToString());
                                    var arduinoFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "programs", "arduino", program.Address.ToString());
                                    Utility.FolderCleanUp(arduinoFolder);
                                    foreach (var newPath in Directory.GetFiles(sourceFolder))
                                    {
                                        File.Copy(newPath, newPath.Replace(sourceFolder, arduinoFolder), true);
                                        LogMessage("* Updating Automation Program: " + program.Name + " (" + program.Address + ") - " + Path.GetFileName(newPath));
                                    }
                                }
                            }
                            catch
                            {
                            }

                            // Add the new program to the ProgramEngine
                            _homegenieService.ProgramManager.ProgramAdd(program);

                            if (!configChanged)
                                configChanged = true;
                        }

                    }

                    if (configChanged)
                    {
                        // Save new programs config
                        _homegenieService.UpdateProgramsDatabase();
                    }
                }

                File.Delete(file);
                if (Directory.Exists(Path.Combine(UpdateBaseFolder, "programs")))
                {
                    Directory.Delete(Path.Combine(UpdateBaseFolder, "programs"), true);
                }
            }
            catch
            {
                success = false;
            }

            if (!success)
            {
                LogMessage("+ ERROR updating Automation Programs");
            }
            return success;
        }

        private bool ProgramsDiff(ProgramBlock oldProgram, ProgramBlock newProgram)
        {
            var unchanged = JsonConvert.SerializeObject(oldProgram.ConditionType) == JsonConvert.SerializeObject(newProgram.ConditionType) &&
                             JsonConvert.SerializeObject(oldProgram.Conditions) == JsonConvert.SerializeObject(newProgram.Conditions) &&
                             JsonConvert.SerializeObject(oldProgram.Commands) == JsonConvert.SerializeObject(newProgram.Commands) &&
                             oldProgram.ScriptCondition == newProgram.ScriptCondition &&
                             oldProgram.ScriptSource == newProgram.ScriptSource &&
                             oldProgram.Name == newProgram.Name &&
                             oldProgram.Description == newProgram.Description &&
                             oldProgram.Group == newProgram.Group &&
                             oldProgram.Type == newProgram.Type;
            return !unchanged;
        }

        private bool UpdateGroups(string file)
        {
            var success = true;
            //
            // add new modules groups
            //
            try
            {
                var serializer = new XmlSerializer(typeof(List<Group>));
                var reader = new StreamReader(file);
                var modulesGroups = (List<Group>)serializer.Deserialize(reader);
                reader.Close();
                //
                var configChanged = false;
                foreach (var group in modulesGroups)
                {
                    if (_homegenieService.Groups.Find(g => g.Name == group.Name) == null)
                    {
                        LogMessage("+ Adding Modules Group: " + group.Name);
                        _homegenieService.Groups.Add(group);
                        if (!configChanged)
                            configChanged = true;
                    }
                }
                //
                if (configChanged)
                {
                    _homegenieService.UpdateGroupsDatabase("");
                }
            }
            catch
            {
                success = false;
            }
            if (!success)
            {
                LogMessage("! ERROR updating Modules Groups");
            }
            return success;
        }

        private bool UpdateAutomationGroups(string file)
        {
            var success = true;
            //
            // add new automation groups
            //
            try
            {
                var serializer = new XmlSerializer(typeof(List<Group>));
                var reader = new StreamReader(file);
                var automationGroups = (List<Group>)serializer.Deserialize(reader);
                reader.Close();
                //
                var configChanged = false;
                foreach (var group in automationGroups)
                {
                    if (_homegenieService.AutomationGroups.Find(g => g.Name == group.Name) == null)
                    {
                        LogMessage("+ Adding Automation Group: " + group.Name);
                        _homegenieService.AutomationGroups.Add(group);
                        if (!configChanged)
                            configChanged = true;
                    }
                }
                //
                if (configChanged)
                {
                    _homegenieService.UpdateGroupsDatabase("Automation");
                }
            }
            catch
            {
                success = false;
            }
            if (!success)
            {
                LogMessage("! ERROR updating Automation Groups");
            }
            return success;
        }

        public bool UpdateScheduler(string file)
        {
            var success = true;
            //
            // add new scheduler items
            //
            try
            {
                var serializer = new XmlSerializer(typeof(List<SchedulerItem>));
                var reader = new StreamReader(file);
                var schedulerItems = (List<SchedulerItem>)serializer.Deserialize(reader);
                reader.Close();
                //
                var configChanged = false;
                foreach (var item in schedulerItems)
                {
                    // it will only import the new ones
                    if (_homegenieService.ProgramManager.SchedulerService.Get(item.Name) == null)
                    {
                        LogMessage("+ Adding Scheduler Item: " + item.Name);
                        _homegenieService.ProgramManager.SchedulerService.AddOrUpdate(item.Name, item.CronExpression, item.Data, item.Description, item.Script);
                        if (!configChanged)
                            configChanged = true;
                    }
                }
                //
                if (configChanged)
                {
                    _homegenieService.UpdateSchedulerDatabase();
                }
            }
            catch
            {
                success = false;
            }
            if (!success)
            {
                LogMessage("! ERROR updating Scheduler Items");
            }
            return success;
        }
        
        public bool UpdateSystemConfig(string file)
        {
            var success = true;
            //
            // add new MIG interfaces
            //
            try
            {
                var serializer = new XmlSerializer(typeof(SystemConfiguration));
                var reader = new StreamReader(file);
                var config = (SystemConfiguration)serializer.Deserialize(reader);

                var configChanged = false;
                foreach (var iface in config.MigService.Interfaces)
                {
                    if (_homegenieService.SystemConfiguration.MigService.GetInterface(iface.Domain) == null)
                    {
                        LogMessage("+ Adding MIG Interface: " + iface.Domain);
                        _homegenieService.SystemConfiguration.MigService.Interfaces.Add(iface);
                        if (!configChanged)
                            configChanged = true;
                    }
                }

                if (configChanged)
                {
                    _homegenieService.SystemConfiguration.Update();
                }
            }
            catch
            {
                success = false;
            }
            if (!success)
            {
                LogMessage("! ERROR updating System Configuration");
            }
            return success;
        }
        
        private void LogMessage(string message)
        {
            InstallProgressMessage?.Invoke(this, message);
        }
    }
}
