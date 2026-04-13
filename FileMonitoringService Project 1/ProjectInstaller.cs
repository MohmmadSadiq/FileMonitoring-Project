using System;
using System.ComponentModel;
using System.Configuration.Install;

namespace FileMonitoringService_Project_1
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : Installer
    {
        private System.ServiceProcess.ServiceProcessInstaller serviceProcessInstaller1;
        private System.ServiceProcess.ServiceInstaller serviceInstaller1;

        public ProjectInstaller()
        {
            InitializeComponent();
            this.serviceProcessInstaller1 = new System.ServiceProcess.ServiceProcessInstaller();
            this.serviceInstaller1 = new System.ServiceProcess.ServiceInstaller();
            // 
            // serviceProcessInstaller1
            // 
            this.serviceProcessInstaller1.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
            this.serviceProcessInstaller1.Password = null;
            this.serviceProcessInstaller1.Username = null;
            // 
            // serviceInstaller1
            // 
            this.serviceInstaller1.Description = "Monitors configured source directories and moves files/folders to destination.";
            this.serviceInstaller1.DisplayName = "File Monitoring Service Project 1";
            this.serviceInstaller1.ServiceName = "FileMonitoringService";
            this.serviceInstaller1.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
            this.serviceInstaller1.ServicesDependedOn = new string[] { "RpcSs", "EventLog", "LanmanWorkstation","Tcpip" };
            // 
            // ProjectInstaller
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.serviceProcessInstaller1,
            this.serviceInstaller1});

        }
    }
}
