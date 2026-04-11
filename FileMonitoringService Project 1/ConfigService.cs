using System;
using System.Collections.Generic;
using System.Configuration;
using System.Collections.Specialized;
using System.IO;

namespace FileMonitoringService_Project_1
{
    internal interface IConfigService
    {
        string DestinationFolder { get; }
        string LogFolder { get; }
        Dictionary<string, string> SourceDirectories { get; }
    }


    /// <summary>
    /// Loads and validates file-monitoring settings from App.config.
    /// </summary>
    /// <remarks>
    /// This class validates path format only. It does not check if a directory exists on disk.
    /// </remarks>
    internal class ConfigService : IConfigService
    {
        private const string DefaultSourceFolder = @"C:\FileMonitoring\Source";
        private const string DefaultDestinationFolder = @"C:\FileMonitoring\Destination";
        private const string DefaultLogFolder = @"C:\FileMonitoring\Logs";

        /// <summary>
        /// Gets the destination folder path where processed files should be moved.
        /// </summary>
        public string DestinationFolder { get; set; }

        /// <summary>
        /// Gets the folder path used to store logs.
        /// </summary>
        public string LogFolder { get; set; }

        /// <summary>
        /// Gets a map of source directory names to directory paths.
        /// </summary>
        public Dictionary<string, string> SourceDirectories { get; set; }

        /// <summary>
        /// Initializes a new instance of <see cref="ConfigService"/> and loads configuration values.
        /// </summary>
        public ConfigService()
        {
            LoadConfig();
        }

        string GetValidLogFolderAndInitLog(string logFolder)
        {
            if (string.IsNullOrWhiteSpace(logFolder))
            {
                AppLogger.Initialize(DefaultLogFolder);
                AppLogger.LogWarning("LogFolder is null or whitespace. Default log folder is used.");
                return DefaultLogFolder;
            }

            try
            {
                string returnedValue = Path.GetFullPath(logFolder.Trim());

                if (IsDefaultValue(returnedValue))
                {
                    if (!string.Equals(returnedValue, DefaultLogFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        AppLogger.LogWarning("A default directory value was detected in App.config for LogFolder: '" + returnedValue + "'. The appropriate default LogFolder was applied: '" + DefaultLogFolder + "'.");
                    }
                    else
                    {
                        AppLogger.LogInfo("Default LogFolder was detected in App.config and applied: '" + DefaultLogFolder + "'.");
                    }

                    AppLogger.Initialize(DefaultLogFolder);
                    return DefaultLogFolder;
                }

                AppLogger.Initialize(returnedValue);
                return returnedValue;
            }
            catch (Exception ex)
            {
                AppLogger.Initialize(DefaultLogFolder);
                AppLogger.LogException(ex, "LogFolder is invalid: '" + logFolder + "'. Default log folder is used.");
                return DefaultLogFolder;
            }
        }


        string GetValidDestinationFolder(string destinationFolder, HashSet<string> directories)
        {
            if (string.IsNullOrWhiteSpace(destinationFolder))
            {
                directories.Add(DefaultDestinationFolder);
                AppLogger.LogWarning("DestinationFolder is null or whitespace. Default destination folder is used.");
                return DefaultDestinationFolder;
            }

            try
            {
                string returnedValue = Path.GetFullPath(destinationFolder.Trim());

                if (IsDefaultValue(returnedValue))
                {
                    if (!string.Equals(returnedValue, DefaultDestinationFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        AppLogger.LogWarning("A default directory value was detected in App.config for DestinationFolder: '" + returnedValue + "'. The appropriate default DestinationFolder was applied: '" + DefaultDestinationFolder + "'.");
                    }
                    else
                    {
                        AppLogger.LogInfo("Default DestinationFolder was detected in App.config and applied: '" + DefaultDestinationFolder + "'.");
                    }

                    returnedValue = DefaultDestinationFolder;
                }

                if (!directories.Add(returnedValue))
                {
                    AppLogger.LogWarning("DestinationFolder duplicates another configured path: '" + returnedValue + "'. Default destination folder is used.");
                    directories.Add(DefaultDestinationFolder);
                    return DefaultDestinationFolder;
                }

                return returnedValue;
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "DestinationFolder is invalid: '" + destinationFolder + "'. Default destination folder is used.");
                directories.Add(DefaultDestinationFolder);
                return DefaultDestinationFolder;
            }
        }

        bool TryGetValidSourceDirectory(string sourceDirectory, string sourceKey, HashSet<string> allDirectories, out string normalizedPath)
        {
            normalizedPath = null;

            if (string.IsNullOrWhiteSpace(sourceDirectory))
            {
                AppLogger.LogWarning("Source directory is null or whitespace. Key='" + sourceKey + "'.");
                return false;
            }

            try
            {
                normalizedPath = Path.GetFullPath(sourceDirectory.Trim());

                if (string.Equals(normalizedPath, DefaultDestinationFolder, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalizedPath, DefaultLogFolder, StringComparison.OrdinalIgnoreCase))
                {
                    AppLogger.LogWarning("Source directory ignored because it matches a default reserved directory value. Key='" + sourceKey + "', Path='" + normalizedPath + "'.");
                    normalizedPath = null;
                    return false;
                }

                if (!allDirectories.Add(normalizedPath))
                {
                    AppLogger.LogWarning("Duplicate source directory rejected. Key='" + sourceKey + "', Path='" + normalizedPath + "'.");
                    normalizedPath = null;
                    return false;
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "Source directory is invalid. Key='" + sourceKey + "', Path='" + sourceDirectory + "'.");
                normalizedPath = null;
                return false;
            }

            return true;
        }

        private void AddDefaultSourceDirectory(HashSet<string> allDirectories, string reason)
        {
            string normalizedDefault = Path.GetFullPath(DefaultSourceFolder);
            if (allDirectories.Add(normalizedDefault))
            {
                SourceDirectories["SourceFolder"] = normalizedDefault;
                AppLogger.LogWarning("Default source directory applied because " + reason + ". SourceFolder='" + normalizedDefault + "'.");
            }
            else
            {
                AppLogger.LogError("Default source directory could not be applied because it conflicts with another configured path. SourceFolder='" + normalizedDefault + "'.");
            }
        }

        bool IsDefaultValue(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return false;
            }

            try
            {
                string normalizedInput = Path.GetFullPath(directoryPath.Trim());

                return string.Equals(normalizedInput, DefaultSourceFolder, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalizedInput, DefaultDestinationFolder, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalizedInput, DefaultLogFolder, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public void LoadConfig()
        {
            try
            {
                LogFolder = GetValidLogFolderAndInitLog(ConfigurationManager.AppSettings["LogFolder"]);

                HashSet<string> allDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                allDirectories.Add(LogFolder);

                DestinationFolder = GetValidDestinationFolder (ConfigurationManager.AppSettings["DestinationFolder"], allDirectories);

                allDirectories.Add(DestinationFolder);

                SourceDirectories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var sourceDirectoriesSection = ConfigurationManager.GetSection("SourceDirectories") as NameValueCollection;

                if (sourceDirectoriesSection == null)
                {
                    AppLogger.LogError("Configuration section 'SourceDirectories' is missing or invalid.");
                    AddDefaultSourceDirectory(allDirectories, "the 'SourceDirectories' section is missing or invalid");
                }
                
                if (sourceDirectoriesSection != null)
                {
                    foreach (string key in sourceDirectoriesSection.AllKeys)
                    {
                        string[] values = sourceDirectoriesSection.GetValues(key);

                        if (values == null || values.Length == 0)
                        {
                            AppLogger.LogWarning("Source directory key has no values and was skipped. Key='" + key + "'.");
                            continue;
                        }

                        string actualPath = values[values.Length - 1];
                        if (TryGetValidSourceDirectory(actualPath, key, allDirectories, out string normalizedPath))
                        {
                            SourceDirectories[key] = normalizedPath;
                        }
                    }

                    if (SourceDirectories.Count == 0)
                    {
                        AddDefaultSourceDirectory(allDirectories, "no valid source directories were found in configuration");
                    }
                }

                AppLogger.LogInfo(
                    "Configuration loaded. LogFolder='" + LogFolder +
                    "', DestinationFolder='" + DestinationFolder +
                    "', SourceDirectoriesCount='" + SourceDirectories.Count + "'.");
            }
            catch (Exception ex)
            {
                ApplyDefaultDirectories();
                AppLogger.LogException(ex, "Invalid configuration detected. Default directories were applied.");
            }
        }

        private void ApplyDefaultDirectories()
        {
            DestinationFolder = DefaultDestinationFolder;
            LogFolder = DefaultLogFolder;
            SourceDirectories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "SourceFolder", DefaultSourceFolder }
            };

            AppLogger.Initialize(LogFolder);
            AppLogger.LogWarning(
                "Using default directories. SourceFolder='" + DefaultSourceFolder +
                "', DestinationFolder='" + DefaultDestinationFolder +
                "', LogFolder='" + DefaultLogFolder + "'.");
        }

       
    }
}
