using System;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using MIG.Config;
using HomeGenie.Service;

namespace HomeGenie.Data
{
    [Serializable]
    public class SystemConfiguration
    {
        private string _passPhrase = "";

        // TODO: change this to use standard event delegates model
        public event Action<bool> OnUpdate;

        public HomeGenieConfiguration HomeGenie { get; set; }

        public MigServiceConfiguration MigService { get; set; }

        public SystemConfiguration()
        {
            HomeGenie = new HomeGenieConfiguration
            {
                SystemName = "HAL",
                Location = "",
                EnableLogFile = "false"
            };
            MigService = new MigServiceConfiguration();
        }

        public bool Update()
        {
            var fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FilePaths.SystemConfigFilePath);
            return UpdateInternal(fileName);
        }

        public bool Update(string fileName)
        {
            return UpdateInternal(fileName);
        }

        private bool UpdateInternal(string fileName)
        {
            var success = false;
            try
            {
                var syscopy = this.DeepClone();
                foreach (var p in syscopy.HomeGenie.Settings)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(p.Value))
                            p.Value = StringCipher.Encrypt(p.Value, GetPassPhrase());
                    }
                    catch
                    {
                    }
                }

                if (File.Exists(fileName))
                    File.Delete(fileName);

                var xmlWriterSettings = new System.Xml.XmlWriterSettings
                {
                    Indent = true,
                    Encoding = Encoding.UTF8
                };
                var xmlSerializer = new XmlSerializer(syscopy.GetType());
                using (var xmlWriter = System.Xml.XmlWriter.Create(fileName, xmlWriterSettings))
                {
                    xmlSerializer.Serialize(xmlWriter, syscopy);
                }
                success = true;
            }
            catch (Exception e)
            {
                MIG.MigService.Log.Error(e);
            }

            OnUpdate?.Invoke(success);

            return success;
        }

        public void SetPassPhrase(string pass)
        {
            _passPhrase = pass;
        }

        public string GetPassPhrase()
        {
            return _passPhrase;
        }
    }
}
