using System;
using System.IO;
using System.Xml.Serialization;

namespace HomeGenie.Service.Updates
{
    public static class UpdatesHelper
    {
        public const string ReleaseFile = "release_info.xml";

        public static ReleaseInfo GetReleaseInfoFromFile(string file)
        {
            ReleaseInfo release = null;
            try
            {
                var serializer = new XmlSerializer(typeof(ReleaseInfo));
                var reader = new StreamReader(file);
                release = (ReleaseInfo)serializer.Deserialize(reader);
                reader.Close();
            }
            catch { }
            return release;
        }

        public static string Format(this Version version)
        {
            return $"V{version.Major}.{version.Minor}.{version.Build}";
        }
    }
}
