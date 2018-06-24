using System;
using System.Collections.Generic;
using System.IO;
using Common.Logging;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers.Zip;

namespace HomeGenie.Utils
{
    internal static class ArchiveHelper
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ArchiveHelper));

        internal static List<string> Unarchive(string archiveName, string destinationFolder)
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
                Log.Error("Unarchive error: " + e.Message, e);
            }

            return extractedFiles;
        }

        internal static void AddFileToZip(string zipFilename, string fileToAdd, string storeAsName = null)
        {
            if(!File.Exists(fileToAdd))
                return;

            var tempZipName = zipFilename + "_tmp";
            try
            {
                if (!File.Exists(zipFilename))
                {
                    ZipArchive.Create().SaveTo(zipFilename, new ZipWriterOptions(CompressionType.Deflate));
                }

                using (var archive = ZipArchive.Open(zipFilename))
                {
                    archive.AddEntry(String.IsNullOrWhiteSpace(storeAsName) ? fileToAdd : storeAsName, fileToAdd);
                    archive.SaveTo(tempZipName, CompressionType.Deflate);
                }
                File.Delete(zipFilename);
                File.Move(tempZipName, zipFilename);
            }
            catch (Exception e)
            {
                Log.Error("Add file to Zip error: " + e.Message, e);
                throw;
            }
        }

        internal static void AddFolderToZip(string zipFilename, string folderPath)
        {
            if(!Directory.Exists(folderPath))
                return;

            try
            {
                if (File.Exists(zipFilename))
                    File.Delete(zipFilename);

                using (var archive = ZipArchive.Create())
                {
                    archive.SafeAddAllFromDirectory(folderPath);
                    archive.SaveTo(zipFilename, CompressionType.Deflate);
                }
            }
            catch (Exception e)
            {
                Log.Error("Add folder to Zip error: " + e.Message, e);
                throw;
            }
        }

        private static void SafeAddAllFromDirectory(
            this IWritableArchive writableArchive,
            string filePath, string searchPattern = "*.*", SearchOption searchOption = SearchOption.AllDirectories)
        {
            foreach (var path in Directory.EnumerateFiles(filePath, searchPattern, searchOption))
            {
                // On Unix we can get FileNotFoundException when getting FileInfo on symlink
                try
                {
                    var fileInfo = new FileInfo(path);
                    writableArchive.AddEntry(path.Substring(filePath.Length), fileInfo.OpenRead(), true, fileInfo.Length,
                        fileInfo.LastWriteTime);
                }
                catch (Exception e)
                {
                    Log.Warn("Can't access file at " + path, e);
                }
            }
        }
    }
}
