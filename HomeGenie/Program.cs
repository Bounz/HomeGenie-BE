using System;
using HomeGenie.Service;
using HomeGenie.Service.Constants;
using MIG;

namespace HomeGenie
{
    public class Program
    {
        private static HomeGenieService Homegenie;
        private static bool IsRunning = true;

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
            Console.WriteLine("\n\nProgram interrupted!\n");
            Quit(false);
        }

        internal static void Quit(bool restartService, bool saveData = true)
        {
            ShutDown(restartService, saveData);
            IsRunning = false;
        }

        private static void ShutDown(bool restart, bool saveData = true)
        {
            Console.Write("HomeGenie is now exiting...\n");

            if (Homegenie != null)
            {
                Homegenie.Stop(saveData);
                Homegenie = null;
            }

            if (restart)
            {
                Console.Write("\n\n...RESTART!\n\n");
                Environment.Exit(1);
            }
            else
            {
                Console.Write("\n\n...QUIT!\n\n");
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


