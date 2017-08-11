using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using VHT_Scanlog_DLL.Models;
using System.Collections.Concurrent;

namespace VHT_Scanlog_DLL
{
    public class ScanlogServiceController
    {
        private static ScanlogConfig Config { get; set; }
        private static FileSystemWatcher ConfigWatcher { get; set; }
        private static List<FileSystemWatcher> LogWatchers { get; set; }
        public static CancellationTokenSource Source { get; set; }
        public static CancellationToken Token { get; set; }
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static Dictionary<string, DateTime> LogWatcherMap = new Dictionary<string, DateTime>();
        public static ConcurrentDictionary<string, FileStatus> CurrentFilesMap = new ConcurrentDictionary<string, FileStatus>();

        public ScanlogServiceController()
        {
            Source = new CancellationTokenSource();
            Token = Source.Token;
            LogWatchers = new List<FileSystemWatcher>();
        }

        public bool Start()
        {
            Log.Info("Starting Scanlog Service");

            var isStarted = true;

            Config = ScanlogServiceConfiguration.ReadConfig();

            if (ScanlogServiceConfiguration.IsConfigValid(Config))
            {
                CreateConfigWatcher();

                List<string> uniqueDirectories = Config.Profiles.Select(d => d.LogDirectory).Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();

                var counter = 0;
                foreach (var dir in uniqueDirectories)
                {
                    if (CreateLogWatcher(dir))
                    {
                        counter++;
                    }
                }

                if (counter == 0)
                {
                    isStarted = false;
                    Log.Fatal("Failed to create FileSystemWatcher for all configured log directories");
                }
            }
            else
            {
                isStarted = false;
            }

            return isStarted;
        }

        private void CreateConfigWatcher()
        {
            Log.Info("Creating File System Watcher for ScanlogConfig.xml");

            ConfigWatcher = new FileSystemWatcher();
            try
            {
                ConfigWatcher.Path = AppDomain.CurrentDomain.BaseDirectory;
                ConfigWatcher.NotifyFilter = NotifyFilters.LastWrite;
                ConfigWatcher.IncludeSubdirectories = false;
                ConfigWatcher.Filter = "*.xml";
                ConfigWatcher.Changed += OnConfigChanged;
                ConfigWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Log.Error("Failed to create FileSystemWatcher for configuration XML. All configuration changes will require a restart of the service");
                ConfigWatcher.Dispose();
            }
        }

        private void OnConfigChanged(object source, FileSystemEventArgs e)
        {
            // TODO filter off of e.ChangeType to avoid triggering twice?

            Log.Info("New ScanlogConfig detected");

            ScanlogConfig newConfig = ScanlogServiceConfiguration.ReadConfig();

            if (ScanlogServiceConfiguration.IsConfigValid(newConfig))
            {
                DisableFileSystemWatchers();
                Log.Warn("Cancelling token source to update configuration. Current file scans will be discarded");
                Source.Cancel();

                Thread.Sleep(10); // To allow current tasks to cancel gracefully

                // TODO stop service if log directories change?
                Config = newConfig;
                Log.Info("Configuration has been successfully updated");

                CancellationTokenSource newSource = new CancellationTokenSource();
                CancellationToken newToken = newSource.Token;
                Source = newSource;
                Token = newToken;
                Log.Info("Token source has been refreshed. New tasks will continue");
                EnableFileSystemWatchers();
            }
            else
            {
                Log.Error("Configuration could not be updated. Stopping Scanlog service");
                Stop();
            }
        }

        private static bool CreateLogWatcher(string dir)
        {
            Log.Info($"Creating File System Watcher for {dir}");
            FileSystemWatcher logWatcher = new FileSystemWatcher();
            try
            {
                logWatcher.Path = dir;
                logWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
                logWatcher.IncludeSubdirectories = true;
                logWatcher.InternalBufferSize = 8192 * 2;
                logWatcher.Created += OnCreatedOrChanged;
                logWatcher.Changed += OnCreatedOrChanged;
                logWatcher.Deleted += OnDeleted;
                logWatcher.Error += OnError;
                logWatcher.EnableRaisingEvents = true;

                LogWatchers.Add(logWatcher);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Log.Error($"Failed to create FileSystemWatcher for LogDirectory: {dir}");
                logWatcher.Dispose();
                return false;
            }
        }

        private static void OnCreatedOrChanged(object source, FileSystemEventArgs e)
        {
            string fileName = Path.GetFileName(e.FullPath);

            Log.Info($"File System Watch detected a new or modified file: {fileName}");

            if (CurrentFilesMap.ContainsKey(fileName))
            {
                FileStatus currentStatus;
                if (CurrentFilesMap.TryGetValue(fileName, out currentStatus))
                {
                    DateTime lastWrite = File.GetLastWriteTime(e.FullPath);
                    if (lastWrite != currentStatus.LastRead) //Prevents duplicate events
                    {
                        if (currentStatus.IsActive)
                        {
                            Log.Info($"There is already an active scan for file {fileName}");
                        }
                        if (!currentStatus.IsActive)
                        {
                            Log.Info($"Changes detected for file {fileName}. Continuing scan from {currentStatus.ReadFileFromHere}");
                            currentStatus.LastRead = lastWrite;
                            StartScan(currentStatus);
                        }
                    }
                }
                else
                {
                    Log.Error($"Could not obtain status for file {fileName}");
                }
            }
            else
            {
                List<string> filesToSearch = GetFilesToSearch(e.FullPath);

                int counter = 0;
                foreach (var file in filesToSearch)
                {
                    if (fileName != null && fileName.ToUpper().Contains(file.ToUpper()))
                    {
                        Log.Info($"Profile for {file} qualifies \"{fileName}\" as a file to scan");
                        try
                        {
                            StartScan(BuildFileStatus(e.FullPath));
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"Failed to create new task to scan file: {file}");
                            Log.Error(ex.Message);
                        }
                    }
                    else
                    {
                        counter++;
                        if (counter == filesToSearch.Count)
                            Log.Trace($"{fileName} is not in the list of files to scan");
                    }
                }
            }
        }

        private static void OnDeleted(object source, FileSystemEventArgs e)
        {
            if (e.FullPath != null)
            {
                FileStatus value;
                string fileName = Path.GetFileName(e.FullPath);
                while (CurrentFilesMap.ContainsKey(fileName))
                {
                    if (CurrentFilesMap.TryRemove(fileName, out value))
                    {
                        Log.Info($"Removed {fileName} from Active File Map");
                    }
                    else
                    {
                        Log.Info($"Could not remove {fileName} from Active File Map. Retrying...");
                    }
                }
            }
        }

        private static void OnError(object source, ErrorEventArgs e)
        {
            if (e.GetException().GetType() == typeof(InternalBufferOverflowException))
            {
                Log.Error("File System Watcher has an internal buffer Overflow" + e.GetException().Message);
            }
        }

        private static List<string> GetFilesToSearch(string path)
        {
            List<string> filesToSearch =
                Config.Profiles.Where(profile => path.ToUpper().Contains(profile.LogDirectory.ToUpper()))
                    .Select(profile => profile.FileName)
                    .Distinct(StringComparer.InvariantCultureIgnoreCase)
                    .ToList();

            return filesToSearch;
        }

        private static void StartScan(FileStatus fileStatus)
        {
            Log.Info($"Starting a new task to begin scanning \"{fileStatus.FileName}\"");

            ScanlogServiceScanner scanner = new ScanlogServiceScanner(Config, fileStatus, Token);
            Task.Run(() => scanner.Start());

            fileStatus.IsActive = true;
            AddOrUpdateStatus(fileStatus);
        }

        private static FileStatus BuildFileStatus(string filePath)
        {
            Log.Info($"Building FileStatus for new file: {filePath}");
            FileStatus fileStatus = new FileStatus
            {
                IsActive = false,
                FileName = Path.GetFileName(filePath),
                FilePath = filePath,
                ReadFileFromHere = 0
            };
            return fileStatus;
        }

        private static void AddOrUpdateStatus(FileStatus fileStatus)
        {
            if (CurrentFilesMap.ContainsKey(fileStatus.FileName))
            {
                FileStatus currentStatus;
                if (CurrentFilesMap.TryGetValue(fileStatus.FileName, out currentStatus))
                {
                    if (CurrentFilesMap.TryUpdate(fileStatus.FileName, fileStatus, currentStatus))
                    {
                        Log.Info($"Successfully updated Current File Map for {fileStatus.FileName}: {fileStatus.IsActive} {fileStatus.FilePath} {fileStatus.ReadFileFromHere}");
                    }
                    else
                    {
                        Log.Error($"Could not update Current File Map for {fileStatus.FileName}: {fileStatus.IsActive} {fileStatus.FilePath} {fileStatus.ReadFileFromHere}");
                    }
                }
                else
                {
                    Log.Error($"Could not update status for {fileStatus.FileName}|Could not get current status");
                }
            }
            else
            {
                if (CurrentFilesMap.TryAdd(fileStatus.FileName, fileStatus))
                {
                    Log.Info($"Successfully added {fileStatus.FileName} to Current File Map: {fileStatus.IsActive} {fileStatus.FilePath} {fileStatus.ReadFileFromHere}");
                }
                else
                {
                    Log.Error($"Could not add {fileStatus.FileName} to Current File Map: {fileStatus.IsActive} {fileStatus.FilePath} {fileStatus.ReadFileFromHere}");
                }
            }
        }

        public void Stop()
        {
            DisposeFileSystemWatchers();
            Log.Info("Cancelling token source");
            Source.Cancel();
        }

        private static void EnableFileSystemWatchers()
        {
            if (ConfigWatcher != null)
            {
                Log.Info("Enabling Scanlog Configuration watcher");
                ConfigWatcher.EnableRaisingEvents = true;
            }

            if (LogWatchers.Any())
            {
                Log.Info($"Enabling {LogWatchers.Count} detected log watchers");

                foreach (var watcher in LogWatchers)
                {
                    watcher.EnableRaisingEvents = true;
                }
            }
        }

        private static void DisableFileSystemWatchers()
        {
            if (ConfigWatcher != null)
            {
                Log.Info("Disabling Scanlog Configuration watcher");
                ConfigWatcher.EnableRaisingEvents = false;
            }

            if (LogWatchers.Any())
            {
                Log.Info($"Disabling {LogWatchers.Count} detected log watchers");

                foreach (var watcher in LogWatchers)
                {
                    watcher.EnableRaisingEvents = false;
                }
            }
        }

        private static void DisposeFileSystemWatchers()
        {
            if (ConfigWatcher != null)
            {
                Log.Info("Disposing of Scanlog Configuration watcher");
                ConfigWatcher.Dispose();
            }

            if (LogWatchers.Any())
            {
                Log.Info($"Disposing of {LogWatchers.Count} detected log watchers");

                foreach (var watcher in LogWatchers)
                {
                    watcher.Dispose();
                }
            }
        }
    }
}
