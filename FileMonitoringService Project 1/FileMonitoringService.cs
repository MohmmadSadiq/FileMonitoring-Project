using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;


namespace FileMonitoringService_Project_1
{
    public partial class FileMonitoringService : ServiceBase
    {
        private readonly FileMonitorHelper _fileMonitorHelper;

        public FileMonitoringService()
            : this(new FileMonitorHelper(new ConfigService()))
        {
        }

        internal FileMonitoringService(FileMonitorHelper fileMonitorHelper)
        {
            _fileMonitorHelper = fileMonitorHelper ?? throw new ArgumentNullException(nameof(fileMonitorHelper));
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                _fileMonitorHelper.InitializeComponent();
                AppLogger.LogInfo("Windows service started file monitoring.");
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "Service failed during OnStart.");
                throw;
            }
        }

        protected override void OnStop()
        {
            try
            {
                _fileMonitorHelper.DisposeWatchers();
                AppLogger.LogInfo("Windows service stopped file monitoring.");
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "Service failed during OnStop.");
            }
        }
    }
}
