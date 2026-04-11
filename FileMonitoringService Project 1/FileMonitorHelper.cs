using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileMonitoringService_Project_1
{
    /// <summary>
    /// Provides file monitoring utilities based on <see cref="FileSystemWatcher"/>.
    /// </summary>
    /// <remarks>
    /// This helper initializes one watcher per configured source directory, listens for
    /// newly created items, then moves files/folders into the configured destination folder.
    /// </remarks>
    internal  class FileMonitorHelper
    {
        private readonly IConfigService _configService;
        private const int FileReadyMaxAttempts = 20;
        private const int FileReadyDelayMs = 300;
        private static readonly TimeSpan DirectoryQuietWindow = TimeSpan.FromSeconds(2);
        private const int DirectoryMaxChecks = 15;

        /// <summary>
        /// Holds active watcher instances so they can be disposed when service stops.
        /// </summary>
        public List<FileSystemWatcher> Watchers { get; }

        public FileMonitorHelper(IConfigService configService)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            Watchers = new List<FileSystemWatcher>();
        }

        /// <summary>
        /// Creates and starts watchers for all source directories from configuration.
        /// </summary>
        /// <remarks>
        /// For each configured source directory:
        /// 1) Ensure the source directory exists.
        /// 2) Create watcher.
        /// 3) Subscribe to Created event.
        /// Also ensures destination directory exists.
        /// </remarks>
        public void InitializeComponent()
        {
            foreach (string directory in _configService.SourceDirectories.Values)
            {
                try
                {
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    FileSystemWatcher watcher = new FileSystemWatcher(directory);

                    // not watching for changes in subdirectories as requirement is to only monitor top-level directories; can be changed to true if needed in future
                    watcher.IncludeSubdirectories = false;

                    // Trigger when a new file/folder is created.
                    watcher.Created += FileSystemWatcher_Create;
                    watcher.Error += FileSystemWatcher_Error;

                    // Start receiving events after handlers are fully wired.
                    watcher.EnableRaisingEvents = true;

                    // Keep reference so we can dispose later.
                    Watchers.Add(watcher);
                }
                catch (Exception ex)
                {
                    AppLogger.LogException(ex, "Failed to initialize watcher for source directory: " + directory);
                }
            }

            try
            {
                if (!Directory.Exists(_configService.DestinationFolder))
                {
                    Directory.CreateDirectory(_configService.DestinationFolder);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "Failed to initialize destination directory: " + _configService.DestinationFolder);
                throw;
            }
        }

        /// <summary>
        /// Handles created items in watched directories.
        /// </summary>
        /// <param name="sender">Watcher that raised the event.</param>
        /// <param name="e">Event data containing item path and type.</param>
        /// <remarks>
        /// If created item is a directory, move+rename directory.
        /// If created item is a file, move file with retry (helps when file is still locked/writing).
        /// </remarks>
        private async void FileSystemWatcher_Create(object sender, FileSystemEventArgs e)
        {
            try 
            {
                AppLogger.LogInfo("Detected created item. Path=" + e.FullPath + "; ChangeType=" + e.ChangeType);

                string extension = Path.GetExtension(e.FullPath);
                string NewName = Guid.NewGuid().ToString() + extension;
                string NewPath = Path.Combine(_configService.DestinationFolder, NewName);

                if (Directory.Exists(e.FullPath))
                {
                    bool isDirectoryStable = await WaitUntilDirectoryStableAsync(e.FullPath, DirectoryQuietWindow, DirectoryMaxChecks);
                    if (!isDirectoryStable)
                    {
                        AppLogger.LogWarning("Directory was not stable before timeout and was skipped: " + e.FullPath);
                        return;
                    }

                    MoveAndRename(e.FullPath, _configService.DestinationFolder, NewName);
                    return;
                }

                if (!File.Exists(e.FullPath))
                {
                    AppLogger.LogWarning("Created event received but source file no longer exists: " + e.FullPath);
                    return;
                }

                bool isFileReady = await WaitUntilFileReadyAsync(e.FullPath, FileReadyMaxAttempts, FileReadyDelayMs);
                if (!isFileReady)
                {
                    AppLogger.LogWarning("File was not ready before timeout and was skipped: " + e.FullPath);
                    return;
                }

                await MoveFileWithRetryAsync(e.FullPath, NewPath);
            }
            catch (Exception ex)
            {
                AppLogger.LogException(ex, "Error handling created item: " + e.FullPath);
            }
          }

        private static async Task<bool> WaitUntilFileReadyAsync(string sourcePath, int maxAttempts, int delayMs)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using (var stream = new FileStream(sourcePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        return true;
                    }
                }
                catch (IOException) when (attempt < maxAttempts)
                {
                    await Task.Delay(delayMs);
                }
                catch (UnauthorizedAccessException) when (attempt < maxAttempts)
                {
                    await Task.Delay(delayMs);
                }
            }

            return false;
        }

        private static async Task<bool> WaitUntilDirectoryStableAsync(string directoryPath, TimeSpan quietWindow, int maxChecks)
        {
            if (!Directory.Exists(directoryPath))
            {
                return false;
            }

            string lastSnapshot = GetDirectorySnapshot(directoryPath);
            if (lastSnapshot == null)
            {
                return false;
            }

            for (int check = 1; check <= maxChecks; check++)
            {
                await Task.Delay(quietWindow);

                if (!Directory.Exists(directoryPath))
                {
                    return false;
                }

                string currentSnapshot = GetDirectorySnapshot(directoryPath);
                if (currentSnapshot == null)
                {
                    return false;
                }

                if (string.Equals(lastSnapshot, currentSnapshot, StringComparison.Ordinal))
                {
                    return true;
                }

                lastSnapshot = currentSnapshot;
            }

            return false;
        }

        private static string GetDirectorySnapshot(string directoryPath)
        {
            try
            {
                long fileCount = 0;
                long totalSize = 0;
                long latestWriteTicks = 0;

                foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
                {
                    var fileInfo = new FileInfo(filePath);
                    fileCount++;
                    totalSize += fileInfo.Length;

                    long lastWrite = fileInfo.LastWriteTimeUtc.Ticks;
                    if (lastWrite > latestWriteTicks)
                    {
                        latestWriteTicks = lastWrite;
                    }
                }

                return string.Concat(fileCount, "|", totalSize, "|", latestWriteTicks);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// Handles underlying watcher errors (for example, internal buffer overflow).
        /// </summary>
        /// <remarks>
        /// Best-effort recovery is applied by restarting the watcher that raised the error.
        /// </remarks>
        private void FileSystemWatcher_Error(object sender, ErrorEventArgs e)
        {
            var watcher = sender as FileSystemWatcher;
            var ex = e.GetException();

            if (watcher == null)
            {
                AppLogger.LogError("FileSystemWatcher error received with unknown sender. " + ex);
                return;
            }

            string overflowHint = ex is InternalBufferOverflowException
                ? " Internal buffer overflow detected; some file events may have been lost."
                : string.Empty;

            AppLogger.LogError("FileSystemWatcher error. Path=" + watcher.Path + "; Message=" + ex.Message + "." + overflowHint);

            try
            {
                // Re-enable to recover from transient watcher failures.
                watcher.EnableRaisingEvents = false;
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception restartEx)
            {
                AppLogger.LogException(restartEx, "Failed to restart watcher for path " + watcher.Path);
            }
        }

        /// <summary>
        /// Moves a file to destination with retry to handle temporary lock/contention.
        /// </summary>
        /// <param name="sourcePath">Source file full path.</param>
        /// <param name="destinationPath">Destination file full path.</param>
        /// <param name="maxAttempts">Maximum number of move attempts.</param>
        /// <param name="cancellationToken">Cancellation token for delay operations.</param>
        /// <exception cref="IOException">
        /// Thrown when move keeps failing after all attempts.
        /// </exception>
        private static async Task MoveFileWithRetryAsync(
            string sourcePath,
            string destinationPath,
            int maxAttempts = 5,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    File.Move(sourcePath, destinationPath);
                    EnsureSourceFileRemoved(sourcePath);
                    AppLogger.LogInfo("File moved successfully. Source=" + sourcePath + "; Destination=" + destinationPath);
                    return;
                }
                catch (IOException) when (attempt < maxAttempts)
                {
                    // Backoff delay before retry if file is busy or not ready.
                    await Task.Delay(200 * attempt, cancellationToken);
                }
                catch (UnauthorizedAccessException) when (attempt < maxAttempts)
                {
                    // Retry when temporary access/permission contention occurs.
                    await Task.Delay(200 * attempt, cancellationToken);
                }
            }

            AppLogger.LogError("Move operation failed after retries. Source=" + sourcePath + "; Destination=" + destinationPath);
            throw new IOException("Failed to move file after multiple attempts.");
        }

        /// <summary>
        /// Moves a directory into destination and renames it in one operation.
        /// </summary>
        /// <param name="sourceDir">Existing source directory.</param>
        /// <param name="destinationParent">Destination parent directory.</param>
        /// <param name="newName">New directory name at destination.</param>
        /// <returns>Final destination directory path.</returns>
        private static string MoveAndRename(string sourceDir, string destinationParent, string newName)
        {
            if (!Directory.Exists(sourceDir))
            {
                throw new DirectoryNotFoundException("Source directory does not exist.");
            }

            if (!Directory.Exists(destinationParent))
            {
                Directory.CreateDirectory(destinationParent);
            }

            string destinationPath = Path.Combine(destinationParent, newName);

            if (Directory.Exists(destinationPath))
            {
                throw new IOException("Destination folder already exists.");
            }

            Directory.Move(sourceDir, destinationPath);
            EnsureSourceDirectoryRemoved(sourceDir);
            AppLogger.LogInfo("Directory moved successfully. Source=" + sourceDir + "; Destination=" + destinationPath);
            return destinationPath;
        }

        private static void EnsureSourceFileRemoved(string sourcePath)
        {
            if (!File.Exists(sourcePath))
            {
                return;
            }

            File.Delete(sourcePath);

            if (File.Exists(sourcePath))
            {
                throw new IOException("Source file still exists after move: " + sourcePath);
            }
        }

        private static void EnsureSourceDirectoryRemoved(string sourceDir)
        {
            if (!Directory.Exists(sourceDir))
            {
                return;
            }

            Directory.Delete(sourceDir, true);

            if (Directory.Exists(sourceDir))
            {
                throw new IOException("Source directory still exists after move: " + sourceDir);
            }
        }

        /// <summary>
        /// Disposes all active watchers and clears the watcher list.
        /// </summary>
        public void DisposeWatchers()
        {
            foreach (var watcher in Watchers)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Created -= FileSystemWatcher_Create;
                    watcher.Error -= FileSystemWatcher_Error;
                    watcher.Dispose();
                }
                catch (Exception ex)
                {
                    AppLogger.LogException(ex, "Failed to dispose watcher for path: " + watcher.Path);
                }
            }

            Watchers.Clear();
        }
    }
}