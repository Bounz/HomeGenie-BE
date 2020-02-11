using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using System.Xml.XPath;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace MIG.Interfaces.HomeAutomation
{
    public class Pepper1Db
    {
        private const string DbFilename = "p1db.xml";
        private const string AdditionalDbFilename = "p1db_custom.xml";
        private const string ArchiveFilename = "archive.zip";
        private const string TempFolder = "temp";
        private const string DefaultPepper1Url = "https://bounz.github.io/HomeGenie-BE/_hg_files/zwave/pepper1_device_archive.zip";

        public bool DbExists
        {
            get
            {
                var dbFile = new FileInfo(GetDbFullPath(DbFilename));
                return dbFile.Exists;
            }
        }

        public bool Update(string pepper1Url = DefaultPepper1Url)
        {
            // request archive from P1 db
            using (var client = new WebClient())
            {
                try
                {
                    MigService.Log.Debug("Downloading archive from {0}.", pepper1Url);
                    client.DownloadFile(pepper1Url, ArchiveFilename);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return false;
                }
            }

            // extract archive
            MigService.Log.Debug("Extracting archive from '{0}' to '{1}' folder.", ArchiveFilename, TempFolder);
            ExtractZipFile(ArchiveFilename, TempFolder);

            MigService.Log.Debug("Creating consolidated DB.");
            var p1Db = new XDocument();
            var dbElement = new XElement("Devices");

            // for each xml file read it content and add to one file
            var files = Directory.GetFiles(TempFolder, "*.xml");
            foreach (var file in files)
            {
                try
                {
                    var fi = new FileInfo(file);
                    var xDoc = XElement.Load(fi.OpenText());
                    dbElement.Add(xDoc.RemoveAllNamespaces());
                }
                catch (Exception)
                {
                }
            }

            p1Db.Add(dbElement);
            var dbFile = new FileInfo(GetDbFullPath(DbFilename));
            using (var writer = dbFile.CreateText())
            {
                p1Db.Save(writer);
            }
            MigService.Log.Debug("DB saved: {0}.", DbFilename);

            Directory.Delete(TempFolder);
            return true;
        }

        /// <summary>
        /// Searches local pepper1 db for the specified device and returns an array of matched device infos in JSON.
        /// </summary>
        /// <returns>The device info.</returns>
        /// <param name="manufacturerId">Manufacturer identifier.</param>
        /// <param name="version">Version (in format appVersion.appSubVersion).</param>
        public string GetDeviceInfo(string manufacturerId, string version)
        {
            var res = GetDeviceInfoInDb(DbFilename, manufacturerId, version);
            // if no devices has been found in pepper1 db, we should try to find them in additional db
            if (res.Count == 0)
            {
                res = GetDeviceInfoInDb(AdditionalDbFilename, manufacturerId, version);
            }

            return JsonConvert.SerializeObject(res, Formatting.Indented, new XmlNodeConverter());
        }

        private List<XElement> GetDeviceInfoInDb(string filename, string manufacturerId, string version)
        {
            var res = new List<XElement>();
            var dbFile = new FileInfo(GetDbFullPath(filename));
            if (!dbFile.Exists)
                return res;
            XDocument db;
            using (var reader = dbFile.OpenText())
            {
                db = XDocument.Load(reader);
            }

            var mIdParts = manufacturerId.Split(new []{ ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (mIdParts.Length != 3)
                throw new ArgumentException(string.Format("Wrong manufacturerId ({0})", manufacturerId));

            var query = string.Format("deviceData/manufacturerId[@value=\"{0}\"] and deviceData/productType[@value=\"{1}\"] and deviceData/productId[@value=\"{2}\"]", mIdParts[0], mIdParts[1], mIdParts[2]);
            if (!string.IsNullOrEmpty(version))
            {
                var vParts = version.Split(new []{ '.' }, StringSplitOptions.RemoveEmptyEntries);
                query += string.Format(" and deviceData/appVersion[@value=\"{0}\"] and deviceData/appSubVersion[@value=\"{1}\"]", vParts[0], vParts[1]);
            }
            var baseQuery = string.Format("//ZWaveDevice[ {0} ]", query);
            res = db.XPathSelectElements(baseQuery).ToList();
            MigService.Log.Debug("Found {0} elements in {1} with query {2}", res.Count, filename, baseQuery);

            if (res.Count == 0)
            {
                // try to find generic device info without version information
                query = string.Format("deviceData/manufacturerId[@value=\"{0}\"] and deviceData/productType[@value=\"{1}\"] and deviceData/productId[@value=\"{2}\"]", mIdParts[0], mIdParts[1], mIdParts[2]);
                baseQuery = string.Format("//ZWaveDevice[ {0} ]", query);
                res = db.XPathSelectElements(baseQuery).ToList();
                MigService.Log.Debug("Found {0} elements in {1} with query {2}", res.Count, filename, baseQuery);
            }

            return res;
        }

        private static string GetDbFullPath(string file)
        {
            var assemblyFolder = Path.GetDirectoryName(typeof(Pepper1Db).Assembly.Location);
            var path = Path.Combine(assemblyFolder, file);
            return path;
        }

        private static void ExtractZipFile(string archiveFilenameIn, string outFolder)
        {
            try
            {
                using (Stream stream = File.OpenRead(archiveFilenameIn))
                using (var reader = ReaderFactory.Open(stream))
                {
                    while (reader.MoveToNextEntry())
                    {
                        if (reader.Entry.IsDirectory)
                            continue;

                        reader.WriteEntryToDirectory(outFolder, new ExtractionOptions
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });
                    }
                }
            }
            catch (Exception e)
            {
                MigService.Log.Error(e, "UnZip error: " + e.Message);
            }
        }
    }
}
