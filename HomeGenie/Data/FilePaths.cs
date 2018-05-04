using System;

namespace HomeGenie.Data
{
    public class FilePaths
    {
        public const string DataFolder = "../data/";

        public const string SystemConfigFileName = "systemconfig.xml";
        public const string SystemConfigFilePath = DataFolder + SystemConfigFileName;

        public const string AutomationProgramsFileName = "automationgroups.xml";
        public const string AutomationProgramsFilePath = DataFolder + AutomationProgramsFileName;

        public const string ModulesFileName = "modules.xml";
        public const string ModulesFilePath = DataFolder + ModulesFileName;

        public const string ProgramsFileName = "programs.xml";
        public const string ProgramsFilePath = DataFolder + ProgramsFileName;

        public const string SchedulerFileName = "scheduler.xml";
        public const string SchedulerFilePath = DataFolder + SchedulerFileName;

        public const string GroupsFileName = "groups.xml";
        public const string GroupsFilePath = DataFolder + GroupsFileName;
        [Obsolete]
        public const string ReleaseInfoFileName = DataFolder + "release_info.xml";

        public const string InstalledPackagesFileName = "installed_packages.json";
        public const string InstalledPackagesFilePath = DataFolder + InstalledPackagesFileName;

        public const string ProgramsFolder = DataFolder + "programs";
    }
}
