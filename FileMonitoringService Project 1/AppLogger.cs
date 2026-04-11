using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace FileMonitoringService_Project_1
{
    internal static class AppLogger
    {
        private static readonly object SyncRoot = new object();
        private const string LogFileName = "MonitorServiceLog.log";
        private static string _logDirectory;

        static AppLogger()
        {
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        }

        public static void Initialize(string configuredLogFolder)
        {
            string resolvedLogDirectory = configuredLogFolder;
            Exception pathResolutionException = null;

            if (string.IsNullOrWhiteSpace(configuredLogFolder))
            {
                resolvedLogDirectory = @"C:\FileMonitoring\Logs";
            }
            else
            {
                try
                {
                    resolvedLogDirectory = Path.GetFullPath(configuredLogFolder.Trim());
                }
                catch (Exception ex)
                {
                    pathResolutionException = ex;
                    resolvedLogDirectory = @"C:\FileMonitoring\Logs";
                }
            }

            lock (SyncRoot)
            {
                _logDirectory = resolvedLogDirectory;
                if(!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }
            }

            if (pathResolutionException != null)
            {
                LogException(pathResolutionException, "Log Directory is not valid. The default directory C:\\FileMonitoring\\Logs is used.");
            }
        }

        public static void LogInfo(string message)
        {
            Write("INFO", message, null);
        }

        public static void LogWarning(string message)
        {
            Write("WARN", message, null);
        }

        public static void LogError(string message)
        {
            Write("ERROR", message, null);
        }

        public static void LogException(Exception exception, string context)
        {
            Write("ERROR", context, exception);
        }

        private static void Write(string level, string message, Exception exception)
        {
            try
            {
                string logFilePath;
                lock (SyncRoot)
                {
                    if (!Directory.Exists(_logDirectory))
                    {
                        Directory.CreateDirectory(_logDirectory);
                    }
                    
                    logFilePath = Path.Combine(_logDirectory, LogFileName);

                    var builder = new StringBuilder();
                    builder.Append(DateTime.UtcNow.ToString("o"));
                    builder.Append(" [");
                    builder.Append(level);
                    builder.Append("] ");
                    builder.Append(message);

                    if (exception != null)
                    {
                        builder.Append(" | Exception=");
                        builder.Append(exception.GetType().FullName);
                        builder.Append(": ");
                        builder.Append(exception.Message);
                        builder.AppendLine();
                        builder.Append(exception.StackTrace);
                    }

                    builder.AppendLine();
                    if(Environment.UserInteractive)
                    {
                        Console.WriteLine(builder.ToString());
                    }
                    File.AppendAllText(logFilePath, builder.ToString());
                }
            }
            catch (Exception loggingException)
            {
                Trace.TraceError("Failed to write log entry: {0}", loggingException.Message);
            }
        }
    }
}
