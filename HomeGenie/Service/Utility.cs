using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.IO;
using System.Threading;
using Common.Logging;
using Newtonsoft.Json;
using HomeGenie.Data;
using HomeGenie.Service.Constants;
using LogManager = Common.Logging.LogManager;

namespace HomeGenie.Service
{
    public static class Utility
    {
        private static ILog Log = LogManager.GetLogger(typeof(Utility));

        // delegate used by RunAsyncTask
        public delegate void AsyncFunction();

        public static dynamic ParseXmlToDynamic(string xml)
        {
            var document = XElement.Load(new StringReader(xml));
            var root = new XElement("Root", document);
            return new DynamicXmlParser(root);
        }

        public static ModuleParameter ModuleParameterGet(Module module, string propertyName)
        {
            if (module == null)
                return null;
            return ModuleParameterGet(module.Properties, propertyName);
        }

        public static ModuleParameter ModuleParameterGet(TsList<ModuleParameter> parameters, string propertyName)
        {
            return parameters.Find(x => x.Name == propertyName);
        }

        public static ModuleParameter ModuleParameterSet(Module module, string propertyName, string propertyValue)
        {
            if (module == null)
                return null;
            return ModuleParameterSet(module.Properties, propertyName, propertyValue);
        }

        public static ModuleParameter ModuleParameterSet(TsList<ModuleParameter> parameters, string propertyName, string propertyValue)
        {
            var parameter = parameters.Find(mpar => mpar.Name == propertyName);
            if (parameter == null)
            {
                parameter = new ModuleParameter() { Name = propertyName, Value = propertyValue };
                parameters.Add(parameter);
            }
            parameter.Value = propertyValue;
            return parameter;
        }

        public static DateTime JavaTimeStampToDateTime(double javaTimestamp)
        {
            // Java timestamp is millisecods past epoch
            var timestamp = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            timestamp = timestamp.AddMilliseconds(javaTimestamp);
            return timestamp;
        }

        public static string Module2Json(Module module, bool hideProperties)
        {
            var settings = new JsonSerializerSettings{ Formatting = Formatting.Indented };
            if (hideProperties)
            {
                var resolver = new IgnorePropertyContractResolver(new List<string>{ "Properties" });
                settings.ContractResolver = resolver;
            }
            return JsonConvert.SerializeObject(module, settings);
        }

        public static string XmlEncode(string fieldValue)
        {
            if (fieldValue == null)
            {
                fieldValue = "";
            }
            else //if (s.IndexOf("&") >= 0 && s.IndexOf("\"") >= 0)
            {
                fieldValue = fieldValue.Replace("&", "&amp;");
                fieldValue = fieldValue.Replace("\"", "&quot;");
            }
            return fieldValue;
        }

        public static string GetTmpFolder()
        {
            var tempFolder = "tmp";
            if (!Directory.Exists(tempFolder))
            {
                Directory.CreateDirectory(tempFolder);
            }
            return tempFolder;
        }

        public static void FolderCleanUp(string path)
        {
            try
            {
                // clean up directory
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
                Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        internal static Thread RunAsyncTask(AsyncFunction functionBlock)
        {
            var asyncTask = new Thread(() =>
            {
                try
                {
                    functionBlock();
                }
                catch (Exception ex)
                {
                    HomeGenieService.LogError(Domains.HomeAutomation_HomeGenie, "Service.Utility.RunAsyncTask", ex.Message, "Exception.StackTrace", ex.StackTrace);
                }
            });
            asyncTask.Start();
            return asyncTask;
        }

        public static DateTime JavascriptToDate(long timestamp)
        {
            var baseDate = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return (baseDate.AddMilliseconds(timestamp));
        }

        public static DateTime JavascriptToDateUtc(double timestamp)
        {
            var baseDate = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Local);
            return (baseDate.AddMilliseconds(timestamp).ToUniversalTime());
        }

        public static double DateToJavascript(DateTime date)
        {
            return (date.Ticks - 621355968000000000L) / 10000D;
        }

        public static double DateToJavascriptLocal(DateTime date)
        {
            return ((date.ToLocalTime().Ticks - 621355968000000000L) / 10000D);
        }

        public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target, bool overwrite = false) {
            foreach (DirectoryInfo dir in source.GetDirectories())
                CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name), overwrite);

            foreach (FileInfo file in source.GetFiles())
                file.CopyTo(Path.Combine(target.FullName, file.Name), overwrite);
        }
    }
}
