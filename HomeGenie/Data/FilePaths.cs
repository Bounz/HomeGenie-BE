using System;

namespace HomeGenie.Data
{
    public class FilePaths
    {
        public const string DataFolder = "../data/";
        public const string LogsFolder = "../logs/";

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

        public const string StatisticsDbFileName = "homegenie_stats.litedb";
        public const string StatisticsDbFilePath = DataFolder + StatisticsDbFileName;

        public const string ProgramsFolder = DataFolder + "programs";
        public const string GatewaysFolder = DataFolder + "gateways";
        public const string InterfacesFolder = DataFolder + "interfaces";
        public const string WidgetsFolder = DataFolder + "widgets";

        // Factory default files
        private const string FactoryConfigDataFolder = "factory_config/";
        public const string DefaultSystemConfigFilePath = FactoryConfigDataFolder + "systemconfig.xml";
        public const string DefaultAutomationGroupsConfigFilePath = FactoryConfigDataFolder + "automationgroups.xml";
        public const string DefaultGroupsConfigFilePath = FactoryConfigDataFolder + "groups.xml";
        public const string DefaultInstalledPackagesConfigFilePath = FactoryConfigDataFolder + "installed_packages.json";
        public const string DefaultModulesConfigFilePath = FactoryConfigDataFolder + "modules.xml";
        public const string DefaultProgramsConfigFilePath = FactoryConfigDataFolder + "programs.xml";
        public const string DefaultSchedulerConfigFilePath = FactoryConfigDataFolder + "scheduler.xml";
        public const string DefaultProgramsFolder = FactoryConfigDataFolder + "programs";
    }
}
