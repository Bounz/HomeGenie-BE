using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace HomeGenie.Data
{
    [Serializable]
    public class HomeGenieConfiguration
    {
        public string GUID { get; set; }
        public string SystemName { get; set; }
        public string Location { get; set; }

        public List<ModuleParameter> Settings = new List<ModuleParameter>();
        public StatisticsConfiguration Statistics = new StatisticsConfiguration();

        public string EnableLogFile { get; set; }

        [Serializable]
        public class StatisticsConfiguration
        {

            [XmlAttribute]
            [Obsolete("This property is not used anymore")]
            public int MaxDatabaseSizeMBytes { get; set; }

            [XmlAttribute]
            public int StatisticsTimeResolutionSeconds { get; set; }

            [XmlAttribute]
            public int StatisticsUIRefreshSeconds { get; set; }

            public StatisticsConfiguration()
            {

                MaxDatabaseSizeMBytes = 5; // 5MB default.
                StatisticsTimeResolutionSeconds = 5 * 60; // 5 minute default.
                StatisticsUIRefreshSeconds = 2 * 60; // 2 minute default.
            }

            /// <summary>
            /// Set constraints to protect the system. These are absolute constraints to protect the user experience (locked browser/server), but are not 
            /// RECOMMENDED constraints. For example, StatisticsTimeResolutionSeconds less than 5*60 starts to make the graph 
            /// look messy, but we still allow anything above 30 seconds in case advanced user wants it. Might want to keep 
            /// recommended values reference later.
            /// 
            /// Should later throw error so UI can notify user?
            /// </summary>
            public void Validate()
            {
                // 
                if (MaxDatabaseSizeMBytes < 1)
                {
                    MaxDatabaseSizeMBytes = 1;
                }
                // Current design would make < 30 seconds a poor setting. In full day view, if this is anything less than a few minutes, day detail line is smashed.
                if (StatisticsTimeResolutionSeconds < 30)
                {
                    StatisticsTimeResolutionSeconds = 30;
                }
                if (StatisticsUIRefreshSeconds < 5)
                {
                    StatisticsUIRefreshSeconds = 5;
                }

            }

        }
    }
}