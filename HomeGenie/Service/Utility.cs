using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using System.Dynamic;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using HomeGenie.Data;
using HomeGenie.Service.Constants;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers.Zip;

namespace HomeGenie.Service
{

    public static class SerializationExtensions
    {
        /// <summary>
        /// Perform a deep Copy of the object.
        /// </summary>
        /// <typeparam name="T">The type of object being copied.</typeparam>
        /// <param name="source">The object instance to copy.</param>
        /// <returns>The copied object.</returns>
        public static T DeepClone<T>(this T source)
        {
            // Don't serialize a null object, simply return the default for that object
            if (ReferenceEquals(source, null))
            {
                return default(T);
            }
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(source));
        }
    }

    public static class DateTimeExtensions
    {
        public static DateTime StartOfWeek(this DateTime dt, DayOfWeek startOfWeek)
        {
            var diff = dt.DayOfWeek - startOfWeek;
            if (diff < 0)
            {
                diff += 7;
            }
            return dt.AddDays(-1 * diff).Date;
        }
    }

    [Serializable()]
    public class TsList<T> : List<T>
    {
        private object syncLock = new object();

        public object LockObject
        {
            get { return syncLock; }
        }

        public new void Clear()
        {
            lock (syncLock)
                base.Clear();
        }

        public new void Add(T value)
        {
            lock (syncLock)
                base.Add(value);
        }

        public new void RemoveAll(Predicate<T> predicate)
        {
            lock (syncLock)
                base.RemoveAll(predicate);
        }

        public new void Remove(T item)
        {
            lock (syncLock)
                base.Remove(item);
        }

        public new void Sort(Comparison<T> comparison)
        {
            lock (syncLock)
                base.Sort(comparison);
        }
    }

    public static class Utility
    {

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

        public static string JsonEncode(string fieldValue)
        {
            if (fieldValue == null)
            {
                fieldValue = "";
            }
            else
            {
                fieldValue = fieldValue.Replace("\\", "\\\\");
                fieldValue = fieldValue.Replace("\"", "\\\"");
                fieldValue = fieldValue.Replace("\n", "\\n");
                fieldValue = fieldValue.Replace("\r", "\\r");
                fieldValue = fieldValue.Replace("\t", "\\t");
                fieldValue = fieldValue.Replace("\b", "\\b");
                fieldValue = fieldValue.Replace("\f", "\\f");
            }
            return fieldValue;
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
            catch
            {
                // TODO: report exception
            }
        }

        private static string picoPath = "/usr/bin/pico2wave";
        public static void Say(string sentence, string locale, bool async = false)
        {
            // if Pico TTS is not installed, then use Google Voice API
            // Note: Pico is only supported in Linux
            if (File.Exists(picoPath) && "#en-us#en-gb#de-de#es-es#fr-fr#it-it#".IndexOf("#"+locale.ToLower()+"#") >= 0)
            {
                if (async)
                {
                    var t = new Thread(() => {
                        PicoSay(sentence, locale);
                    });
                    t.Start();
                }
                else
                {
                    PicoSay(sentence, locale);
                }
            }
            else
            {
                if (async)
                {
                    var t = new Thread(() => {
                        GoogleVoiceSay(sentence, locale);
                    });
                    t.Start();
                }
                else
                {
                    GoogleVoiceSay(sentence, locale);
                }
            }
        }

        public static void Play(string wavFile)
        {

            var os = Environment.OSVersion;
            var platform = os.Platform;
            //
            switch (platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.WinCE:
                PlaySound(wavFile, UIntPtr.Zero, (uint)(0x00020000 | 0x00000000));
                break;
                case PlatformID.Unix:
                case PlatformID.MacOSX:
                default:
                //var player = new System.Media.SoundPlayer();
                //player.SoundLocation = wavFile;
                //player.Play();
                Process.Start(new ProcessStartInfo("aplay", "\"" + wavFile + "\"") {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = false
                }).WaitForExit();
                break;
            }

        }

        #region Private helper methods

        [DllImport("winmm.dll", SetLastError = true)]
        static extern bool PlaySound(string pszSound, UIntPtr hmod, uint fdwSound);
        // buffer size for AddFileToZip
        private const long BUFFER_SIZE = 4096;
        // delegate used by RunAsyncTask
        public delegate void AsyncFunction();

        internal static void PicoSay(string sentence, string locale)
        {
            try
            {
                var wavFile = Path.Combine(GetTmpFolder(), "_synthesis_tmp.wav");
                if (File.Exists(wavFile))
                    File.Delete(wavFile);

                Process.Start(new ProcessStartInfo(picoPath, " -w " + wavFile + " -l " + locale + " \"" + sentence + "\"") {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = false
                }).WaitForExit();

                if (File.Exists(wavFile))
                    Play(wavFile);
            }
            catch (Exception e)
            {
                HomeGenieService.LogError(e);
            }
        }

        internal static void GoogleVoiceSay(string sentence, string locale)
        {
            try
            {
                var mp3File = Path.Combine(GetTmpFolder(), "_synthesis_tmp.mp3");
                using (var client = new WebClient())
                {
                    client.Encoding = Encoding.UTF8;
                    client.Headers.Add("Referer", "http://translate.google.com");
                    var audioData = client.DownloadData("http://translate.google.com/translate_tts?ie=UTF-8&tl=" + Uri.EscapeDataString(locale) + "&q=" + Uri.EscapeDataString(sentence) + "&client=homegenie&ts=" + DateTime.UtcNow.Ticks);

                    if (File.Exists(mp3File))
                        File.Delete(mp3File);

                    var stream = File.OpenWrite(mp3File);
                    stream.Write(audioData, 0, audioData.Length);
                    stream.Close();

                    client.Dispose();
                }

                var wavFile = mp3File.Replace(".mp3", ".wav");
                Process.Start(new ProcessStartInfo("lame", "--decode \"" + mp3File + "\" \"" + wavFile + "\"") {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = false
                }).WaitForExit();

                if (File.Exists(mp3File))
                    Play(wavFile);
            }
            catch (Exception e)
            {
                HomeGenieService.LogError(e);
            }
        }

        internal static List<string> UncompressTgz(string archiveName, string destinationFolder)
        {
            var extractedFiles = new List<string>();
            try
            {
                using (Stream stream = File.OpenRead(archiveName))
                using (var reader = ReaderFactory.Open(stream))
                {
                    while (reader.MoveToNextEntry())
                    {
                        if (reader.Entry.IsDirectory)
                            continue;

                        extractedFiles.Add(reader.Entry.Key);
                        reader.WriteEntryToDirectory(destinationFolder, new ExtractionOptions
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("UnTar error: " + e.Message);
            }

            return extractedFiles;
        }

        internal static List<string> UncompressZip(string archiveName, string destinationFolder)
        {
            var extractedFiles = new List<string>();
            try
            {
                using (Stream stream = File.OpenRead(archiveName))
                using (var reader = ReaderFactory.Open(stream))
                {
                    while (reader.MoveToNextEntry())
                    {
                        if (reader.Entry.IsDirectory)
                            continue;

                        extractedFiles.Add(reader.Entry.Key);
                        reader.WriteEntryToDirectory(destinationFolder, new ExtractionOptions
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("UnZip error: " + e.Message);
            }

            return extractedFiles;
        }

        internal static void AddFileToZip(string zipFilename, string fileToAdd, string storeAsName = null)
        {
            // TODO it may be useful to prepare temp directory and use AddAllFromDirectory method
            // rather then add files one by one
            // archive.AddAllFromDirectory(@"C:\source");

            var tempZipName = zipFilename + "_tmp";
            try
            {
                if (!File.Exists(zipFilename))
                {
                    ZipArchive.Create().SaveTo(zipFilename, new ZipWriterOptions(CompressionType.Deflate));
                }

                using (var archive = ZipArchive.Open(zipFilename))
                {
                    archive.AddEntry(string.IsNullOrWhiteSpace(storeAsName) ? fileToAdd : storeAsName, fileToAdd);
                    archive.SaveTo(tempZipName, CompressionType.Deflate);
                }
                File.Delete(zipFilename);
                File.Move(tempZipName, zipFilename);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
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

        #endregion

    }

    public class DynamicXmlParser : DynamicObject
    {

        XElement element;

        public DynamicXmlParser(string filename)
        {
            element = XElement.Load(filename);
        }

        public DynamicXmlParser(XElement el)
        {
            element = el;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            if (element == null)
            {
                result = null;
                return false;
            }

            var sub = element.Element(binder.Name);
            if (sub == null)
            {
                result = null;
                return false;
            }
            else
            {
                result = new DynamicXmlParser(sub);
                return true;
            }
        }

        public static implicit operator string(DynamicXmlParser p)
        {
            return p.ToString();
        }

        public override string ToString()
        {
            if (element != null)
            {
                return element.Value;
            }
            else
            {
                return string.Empty;
            }
        }

        public string this[string attr]
        {
            get
            {
                if (element == null)
                {
                    return string.Empty;
                }
                return element.Attribute(attr).Value;
            }
        }
    }

    public class ConsoleRedirect : TextWriter
    {
        private string lineBuffer = "";

        public Action<string> ProcessOutput;

        public override void Write(string message)
        {
            var newLine = new string(CoreNewLine);
            if (message.IndexOf(newLine) >= 0)
            {
                var parts = message.Split(CoreNewLine);
                if (message.StartsWith(newLine))
                    WriteLine(lineBuffer);
                else
                    parts[0] = lineBuffer + parts[0];
                lineBuffer = "";
                if (parts.Length > 1 && !parts[parts.Length - 1].EndsWith(newLine))
                {
                    lineBuffer += parts[parts.Length - 1];
                    parts[parts.Length - 1] = "";
                }
                foreach (var s in parts)
                {
                    if (!String.IsNullOrWhiteSpace(s))
                        WriteLine(s);
                }
                message = "";
            }
            lineBuffer += message;
        }
        public override void WriteLine(string message)
        {
            if (ProcessOutput != null && !string.IsNullOrWhiteSpace(message))
            {
                // log entire line into the "Domain" column
                //SystemLogger.Instance.WriteToLog(new HomeGenie.Data.LogEntry() {
                //    Domain = "# " + this.lineBuffer + message
                //});
                ProcessOutput(lineBuffer + message);
            }
            lineBuffer = "";
        }

        public override Encoding Encoding
        {
            get
            {
                return Encoding.UTF8;
            }
        }

    }

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
                jsonProperty.ShouldSerialize = instance => {return false;};
            return jsonProperty;
        }
    }
}
