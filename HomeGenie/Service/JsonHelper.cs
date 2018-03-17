using System.Net.Mail;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace HomeGenie.Service
{
    public static class JsonHelper
    {
        public static string ToPrettyJson(this object obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented);
        }

        public static string ToPrettyJson(this object obj, IContractResolver resolver)
        {
            return JsonConvert.SerializeObject(obj, new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = resolver
            });
        }
    }

    public class MailMessageConstractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            if (property.DeclaringType == typeof(MailMessage) && property.PropertyName == "Attachments")
            {
                property.ShouldSerialize = o => false;
            }

            return property;
        }
    }
}
