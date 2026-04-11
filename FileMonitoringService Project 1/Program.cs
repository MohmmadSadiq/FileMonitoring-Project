using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace FileMonitoringService_Project_1
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            try
            {
                IConfigService configService = new ConfigService();
                AppLogger.LogInfo("Application startup initialized.");

                var fileMonitorHelper = new FileMonitorHelper(configService);

                if(Environment.UserInteractive)
                {
                    fileMonitorHelper.InitializeComponent();
                    AppLogger.LogInfo("Interactive monitoring started.");
                    Console.WriteLine("Monitoring started. Press Enter to stop.");
                    Console.ReadLine();
                    fileMonitorHelper.DisposeWatchers();
                    AppLogger.LogInfo("Interactive monitoring stopped.");
                }
                else
                {
                    ServiceBase[] ServicesToRun;
                    ServicesToRun = new ServiceBase[]
                    {
                        new FileMonitoringService(fileMonitorHelper)
                    };
                    ServiceBase.Run(ServicesToRun);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "Fatal error during application startup.");
                Environment.ExitCode = 1;
            }
        }
    }
}
