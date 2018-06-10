using Newtonsoft.Json;

namespace HomeGenie.Service.Packages
{
    public class PackageProgramDefinition
    {
        [JsonProperty("file")]
        public string File { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("uid")]
        public string Uid { get; set; }

        [JsonProperty("group")]
        public string Group { get; set; }
    }
}
