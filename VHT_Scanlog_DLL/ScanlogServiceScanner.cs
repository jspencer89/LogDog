using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NLog;
using VHT_Scanlog_DLL.Models;

namespace VHT_Scanlog_DLL
{
    public class ScanlogServiceScanner
    {
        private bool _shouldStop;
        private int _totalBytesRead;
        private FileStatus CurrentStatus;
        private static ScanlogConfig Config { get; set; }
        private static CancellationToken Token { get; set; }
        private readonly object _readLock = new object();
        private readonly object _isOpenLock = new object();
        private readonly Logger _log = LogManager.GetCurrentClassLogger();

        public ScanlogServiceScanner(ScanlogConfig config, FileStatus currentStatus, CancellationToken token)
        {
            Config = config;
            CurrentStatus = currentStatus;
            Token = token;
        }

        public void Start()
        {
            _shouldStop = false;
            List<string> stringsToSearch = GetStringsToSearch();

            if (stringsToSearch.Any())
            {
                _log.Info($"Begin scanning file: {CurrentStatus.FilePath}");
            }
            else
            {
                _log.Info($"Exhausted list of StringsToSearch for file: {CurrentStatus.FilePath}");
                Stop();
            }            
            
            while (!_shouldStop && !Token.IsCancellationRequested)
            {
                string chunkText;
                var bytesRead = ReadAChunk(out chunkText);

                if (bytesRead == 0 && !IsFileOpen(CurrentStatus.FilePath))
                {
                    Stop();
                }

                if (bytesRead != 0)
                {
                    _totalBytesRead += bytesRead;
                    
                    for (int index = 0; index < stringsToSearch.Count; index++)
                    {
                        var str = stringsToSearch[index];
                        if (chunkText.ToUpper().Contains(str.ToUpper()))
                        {
                            _log.Info($"The string {str} was found in {CurrentStatus.FilePath}");

                            ScanlogServiceActions scanlogServiceActions = new ScanlogServiceActions(Config);
                            scanlogServiceActions.ProcessAction(str, CurrentStatus.FilePath);

                            _log.Info($"Removing {str} from subsequent scans of file: {CurrentStatus.FilePath}");

                            stringsToSearch.RemoveAt(index);
                            index--;
                        }
                    }

                    if (stringsToSearch.Count == 0)
                    {
                        _log.Info($"Exhausted list of StringsToSearch for file: {CurrentStatus.FilePath}");
                        Stop();
                    }
                }
            }
            UpdateFileStatus();
            _log.Info("Task successfully terminated");
        }

        private List<string> GetStringsToSearch()
        {            
            List<string> stringsToSearch = (from profile in Config.Profiles where CurrentStatus.FilePath.ToUpper().Contains(profile.FileName.ToUpper()) select profile.StringToSearch).ToList();

            for (int index = 0; index < stringsToSearch.Count; index++)
            {
                if (CurrentStatus.StringsFound.Contains(stringsToSearch[index]))
                {
                    stringsToSearch.RemoveAt(index);
                    index--;
                }
            }
            return stringsToSearch;
        }

        private int ReadAChunk(out string chunkText)
        {
            lock (_readLock)
            {
                int bytesRead = 0;
                var fileInfo = new FileInfo(CurrentStatus.FilePath);
                int chunkSize = Convert.ToInt32(Config.ChunkSize);
                byte[] bytes = new byte[Convert.ToInt32(Config.ChunkSize)];
                chunkText = null;

                do
                {
                    try
                    {
                        using (FileStream fileStream = new FileStream(CurrentStatus.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            if (_totalBytesRead >= fileStream.Length)
                            {
                                _log.Info($"Reached the end of the File Stream for file {CurrentStatus.FilePath}");
                                return 0;
                            }

                            fileStream.Seek(CurrentStatus.ReadFileFromHere, SeekOrigin.Begin);
                            bytesRead = fileStream.Read(bytes, 0, chunkSize);

                            if (fileInfo.Length == 0 && IsFileOpen(CurrentStatus.FilePath))
                            {
                                Thread.Sleep(500);
                            }
                            fileInfo.Refresh();
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex.Message);
                    }
                }
                while (IsFileOpen(CurrentStatus.FilePath) && (fileInfo.Length == 0) && !Token.IsCancellationRequested);  // if file is still open and no bytes were read, wait.

                if (bytesRead == 0)
                {
                    _log.Error($"Could not read from file {CurrentStatus.FilePath}");
                    return 0;
                }

                _log.Trace($"Successfully read {bytesRead} from file {CurrentStatus.FilePath} position {CurrentStatus.ReadFileFromHere}");

                chunkText = Encoding.ASCII.GetString(bytes);

                var index = chunkText.LastIndexOf('\n');

                if (index != -1)
                {
                    CurrentStatus.ReadFileFromHere += index + 1; // next file read starts from here
                    chunkText = chunkText.Remove(index, bytesRead - index - 1);
                }
                else
                {
                    CurrentStatus.ReadFileFromHere = bytesRead;
                }
                return bytesRead;
            }//lock()
        }

        private bool IsFileOpen(string filePath)
        {
            lock (_isOpenLock)
            {
                try
                {
                    FileStream fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write);
                    fileStream.Close();
                    return false;
                }
                catch (Exception)
                {
                    return true;
                }
            }
        }

        private void UpdateFileStatus()
        {
            if (CurrentStatus.FilePath != null)
            {
                CurrentStatus.IsActive = false;
                //TODO: Check if Map contains key first
                FileStatus originalStatus;
                ScanlogServiceController.CurrentFilesMap.TryGetValue(Path.GetFileName(CurrentStatus.FilePath), out originalStatus);

                _log.Info(ScanlogServiceController.CurrentFilesMap.TryUpdate(Path.GetFileName(CurrentStatus.FilePath), CurrentStatus, originalStatus)
                        ? $"Successfully updated Current File Map for {Path.GetFileName(CurrentStatus.FilePath)}. IsActive: {CurrentStatus.IsActive}, StartScanFromHere: {CurrentStatus.ReadFileFromHere}"
                        : $"Could not update Current File Map for {Path.GetFileName(CurrentStatus.FilePath)}");
            }
        }

        private void Stop()
        {
            _log.Info("Requesting graceful termination of task");
            _shouldStop = true;
        }
    }
}
