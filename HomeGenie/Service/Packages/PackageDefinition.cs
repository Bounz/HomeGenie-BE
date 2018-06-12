using System;
using Newtonsoft.Json;

namespace HomeGenie.Service.Packages
{
    public class PackageDefinition
    {
        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("published")]
        public DateTimeOffset Published { get; set; }

        [JsonProperty("sourcecode")]
        public string Sourcecode { get; set; }

        [JsonProperty("homepage")]
        public string Homepage { get; set; }

        [JsonProperty("widgets")]
        public PackageInterfaceDefinition[] Widgets { get; set; }

        [JsonProperty("programs")]
        public PackageProgramDefinition[] Programs { get; set; }

        [JsonProperty("interfaces")]
        public PackageInterfaceDefinition[] Interfaces { get; set; }

        [JsonProperty("folder_url")]
        public string SourceUrl { get; set; }

        [JsonProperty("install_date")]
        public DateTime InstallDate { get; set; }
    }
}
