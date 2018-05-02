using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace HomeGenie.Service
{
    public class IgnorePropertyContractResolver : DefaultContractResolver
    {
        private readonly List<string> _ignoredProperties;

        public IgnorePropertyContractResolver(List<string> ignoredProperties)
        {
            _ignoredProperties = ignoredProperties;
        }

        protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
        {
            var jsonProperty = base.CreateProperty(member, memberSerialization);
            if (_ignoredProperties.Contains(member.Name))
                jsonProperty.ShouldSerialize = instance => false;
            return jsonProperty;
        }
    }
}
