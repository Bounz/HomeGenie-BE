using System;

namespace HomeGenie.Data
{
    public class FilePaths
    {
        public const string DataFolder = "../data/";

        public const string SystemConfigFileName = DataFolder + "systemconfig.xml";
        public const string AutomationProgramsFileName = DataFolder + "automationgroups.xml";
        public const string ModulesFileName = DataFolder + "modules.xml";
        public const string ProgramsFileName = DataFolder + "programs.xml";
        public const string SchedulerFileName = DataFolder + "scheduler.xml";
        public const string GroupsFileName = DataFolder + "groups.xml";
        [Obsolete]
        public const string ReleaseInfoFileName = DataFolder + "release_info.xml";

        public const string ProgramsFolder = DataFolder + "programs";
    }
}
