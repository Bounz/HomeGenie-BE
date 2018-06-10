using Newtonsoft.Json;

namespace HomeGenie.Service.Packages
{
    public class PackageInterfaceDefinition
    {
        [JsonProperty("file")]
        public string File { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }
    }
}
