using System;

namespace HomeGenie.Service.Updates
{
    [Serializable]
    public class ReleaseInfo
    {
        public string Version { get; set; }
        public string Description { get; set; }
        public string ReleaseNote { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string DownloadUrl;
    }
}
