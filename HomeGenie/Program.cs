using System;
using System.Threading;
using HomeGenie.Service;
using HomeGenie.Service.Constants;
using HomeGenie.Utils;
using MIG;
using NLog;

namespace HomeGenie
{
    public static class Program
    {
        private static HomeGenieService Homegenie;
        private static bool IsRunning = true;

        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;
            AppDomain.CurrentDomain.SetupInformation.ShadowCopyFiles = "true";

            if (SignalWaiter.Instance.CanWaitExitSignal())
                new Thread(() => SignalWaiter.Instance.WaitExitSignal(TerminateOnUnixSignal)).Start();
            else
                Console.CancelKeyPress += TerminateOnCancelKeyPress;

            Homegenie = new HomeGenieService();
            do { Thread.Sleep(2000); } while (IsRunning);
        }

        private static void TerminateOnUnixSignal()
        {
            Log.Info("Got UNIX signal");
            Log.Info("Program interrupted!");
            Quit(false);
        }

        private static void TerminateOnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Log.Info($"Got signal {(e.SpecialKey == ConsoleSpecialKey.ControlC ? "Ctrl+C" : "Ctrl+Break")}");
            Log.Info("Program interrupted!");
            Quit(false);
        }

        internal static void Quit(bool restartService, bool saveData = true)
        {
            ShutDown(restartService, saveData);
            IsRunning = false;
        }

        internal static void QuitAndUpdateDockerImage()
        {
            Log.Info("...RESTARTING WITH DOCKER IMAGE UPDATE!");
            Environment.Exit(5);
        }

        private static void ShutDown(bool restart, bool saveData = true)
        {
            Log.Info("HomeGenie is now exiting...");

            if (Homegenie != null)
            {
                Homegenie.Stop(saveData);
                Homegenie = null;
            }

            if (restart)
            {
                Log.Info("...RESTART!");
                Environment.Exit(1);
            }
            else
            {
                Log.Info("...QUIT!");
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
