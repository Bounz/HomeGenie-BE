using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Reflection;
using HomeGenie.Data;

namespace HomeGenie.Service.Logging
{
    /// <inheritdoc />
    /// <summary>
    /// A Logging class implementing the Singleton pattern and an internal Queue to be flushed perdiodically
    /// </summary>
    public class SystemLogger : IDisposable
    {
        private readonly Queue<string> _logQueue;
        private const int MaxLogAge = (60 * 60 * 24) * 1; // one day
        private const int QueueSize = 50;
        private FileStream _logStream;
        private StreamWriter _logWriter;
        private readonly StreamWriter _standardOutput;
        private DateTime _lastFlushed = DateTime.Now;

        /// <summary>
        /// Private constructor to prevent instance creation
        /// </summary>
        private SystemLogger()
        {
            _standardOutput = new StreamWriter(Console.OpenStandardOutput()) {AutoFlush = true};
            _logQueue = new Queue<string>();
        }

        /// <summary>
        /// An LogWriter instance that exposes a single instance
        /// </summary>
        private static readonly Lazy<SystemLogger> LazyInstance = new Lazy<SystemLogger>(() => new SystemLogger(), true);

        public static SystemLogger Instance => LazyInstance.Value;

        /// <summary>
        /// The single instance method that writes to the log file
        /// </summary>
        /// <param name="logEntry"></param>
        public void WriteToLog(string logEntry)
        {
            _standardOutput.WriteLine(logEntry);
            ThreadPool.QueueUserWorkItem(new WaitCallback((state)=>{
                lock (_logQueue)
                {
                    // Lock the queue while writing to prevent contention for the log file
                    _logQueue.Enqueue(logEntry);
                    // If we have reached the Queue Size then flush the Queue
                    if (_logQueue.Count >= QueueSize || DoPeriodicFlush())
                    {
                        FlushLog();
                    }
                }
            }));
        }

        private bool DoPeriodicFlush()
        {
            var logAge = DateTime.Now - _lastFlushed;
            if (logAge.TotalSeconds >= MaxLogAge)
            {
                _lastFlushed = DateTime.Now;
                //TODO: rename file with timestamp, compress it and open a new one
                // or simply keep max 2 days renaming old one to <logfile>.old
                CloseLog();

                var assembly = Assembly.GetExecutingAssembly();
                var logFile = assembly.ManifestModule.Name.ToLower().Replace(".exe", ".log");
                var logPath = Path.Combine(FilePaths.LogsFolder, logFile);
                var logFileBackup = logPath + ".bak";
                if (File.Exists(logFileBackup))
                    File.Delete(logFileBackup);

                File.Move(logPath, logFileBackup);

                OpenLog();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Flushes the Queue to the physical log file
        /// </summary>
        public void FlushLog()
        {
            try
            {
                while (_logQueue.Count > 0)
                {
                    var entry = _logQueue.Dequeue();
                    _logWriter.WriteLine(entry);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: LogWriter could not flush log - " + e.Message + "\n" + e.StackTrace);
            }
        }

        public void OpenLog()
        {
            CloseLog();

            var assembly = Assembly.GetExecutingAssembly();
            var versionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            var version = versionInfo.FileVersion;
            var logFile = assembly.ManifestModule.Name.ToLower().Replace(".exe", ".log");
            var logPath = Path.Combine(FilePaths.LogsFolder, logFile);
            if (!Directory.Exists(FilePaths.LogsFolder))
            {
                Directory.CreateDirectory(FilePaths.LogsFolder);
            }
            _logStream = File.Open(logPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            _logWriter = new StreamWriter(_logStream);
            _logWriter.WriteLine("#Version: 1.0");
            _logWriter.WriteLine("#Software: " + assembly.ManifestModule.Name.Replace(".exe", "") + " " + version);
            _logWriter.WriteLine("#Start-Date: " + DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz"));
            _logWriter.WriteLine("#Fields: datetime\tsource-domain\tsource-id\tdescription\tproperty\tvalue\n");
            _logQueue.Clear();
        }

        public void CloseLog()
        {
            if (IsLogEnabled)
            {
                try
                {
                    FlushLog();
                    _logWriter.Close();
                    _logWriter = null;
                    _logStream.Close();
                    _logStream = null;
                }
                catch
                {
                }
            }
        }

        public bool IsLogEnabled => _logStream != null && _logWriter != null;

        public void Dispose()
        {
            if (LazyInstance.IsValueCreated)
            {
                CloseLog();
            }
        }
    }
}
