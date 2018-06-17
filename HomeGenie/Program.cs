using System;
using HomeGenie.Service;
using HomeGenie.Service.Constants;
using MIG;
using NLog;

namespace HomeGenie
{
    public class Program
    {
        private static HomeGenieService Homegenie;
        private static bool IsRunning = true;

        private static Logger _log = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;
            AppDomain.CurrentDomain.SetupInformation.ShadowCopyFiles = "true";

            Console.CancelKeyPress += Console_CancelKeyPress;

            Homegenie = new HomeGenieService();
            do { System.Threading.Thread.Sleep(2000); } while (IsRunning);
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            _log.Info("Program interrupted!");
            _log.Info($"Got signal {(e.SpecialKey == ConsoleSpecialKey.ControlC ? "Ctrl+C" : "Ctrl+Break")}");
            Quit(false);
        }

        internal static void Quit(bool restartService, bool saveData = true)
        {
            ShutDown(restartService, saveData);
            IsRunning = false;
        }

        private static void ShutDown(bool restart, bool saveData = true)
        {
            _log.Info("HomeGenie is now exiting...");

            if (Homegenie != null)
            {
                Homegenie.Stop(saveData);
                Homegenie = null;
            }

            if (restart)
            {
                _log.Info("...RESTART!");
                Environment.Exit(1);
            }
            else
            {
                _log.Info("...QUIT!");
                Environment.Exit(0);
            }
        }

        private static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e) 
        {
            // logger of last hope
            if (e.ExceptionObject is Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

            var logEntry = new MigEvent(
                Domains.HomeAutomation_HomeGenie,
                "Trapper",
                "Unhandled Exception",
                "Error.Exception",
                e.ExceptionObject.ToString()
            );
            try
            {
                // try broadcast first (we don't want homegenie object to be passed, so use the domain string)
                Homegenie.RaiseEvent(Domains.HomeGenie_System, logEntry);
            }
            catch 
            {
                HomeGenieService.LogError(logEntry);
            }
        }
    }
}
