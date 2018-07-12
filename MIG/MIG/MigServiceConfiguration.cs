using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace MIG.Config
{

    [Serializable()]
    public class MigServiceConfiguration
    {
        public List<Gateway> Gateways = new List<Gateway>();

        public List<Interface> Interfaces = new List<Interface>();

        public Interface GetInterface(string domain)
        {
            return this.Interfaces.Find(i => i.Domain.Equals(domain));
        }

        public Gateway GetGateway(string name)
        {
            return this.Gateways.Find(g => g.Name.Equals(name));
        }
    }

    [Serializable]
    public class Gateway
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public bool IsEnabled { get; set; }

        public List<Option> Options = new List<Option>();
    }

    [Serializable]
    public class Option
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public string Value { get; set; }

        public Option()
        {
        }

        public Option(string name, string value = "")
        {
            Name = name;
            Value = value;
        }
    }

    [Serializable]
    public class Interface
    {

        [XmlAttribute]
        public string Domain { get; set; }

        public string Description { get; set; }

        [XmlAttribute]
        public bool IsEnabled { get; set; }

        public List<Option> Options = new List<Option>();

        [XmlAttribute]
        public string AssemblyName { get; set; }

        // TODO: add SupportedPlatform field (Windows, Unix, All)
    }
}

