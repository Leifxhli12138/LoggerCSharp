using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;

namespace LoggerCSharp.LogUtil.Writers
{
    public class FileWriter : ILogWritter
    {
        private readonly string _fileBaseName;
        private readonly string _directoryPath;
        private readonly ConcurrentQueue<LogEntry> _logQueue = new ConcurrentQueue<LogEntry>();
        private readonly AutoResetEvent _writeLogEvent = new AutoResetEvent(false);
        private readonly Thread _writeLogThread;

        private string _filePath;
        private bool _exit = false;
        private long _currentLogSize = -1;

        public FileWriter(string fileBaseName = "")
        {
            _fileBaseName = fileBaseName;
            _directoryPath = AppDomain.CurrentDomain.BaseDirectory + "\\Logs\\" + _fileBaseName;

            _writeLogThread = new Thread(LoggingThread)
            {
                Name = "TextLoggerThread",
                IsBackground = true,
            };
            _writeLogThread.Start();
        }

        public void WriteMessage(LogEntry logEntry)
        {
            _logQueue.Enqueue(logEntry);
            _writeLogEvent.Set();
        }

        public void Stop()
        {
            _exit = true;
            _writeLogEvent.Set();
            _writeLogThread.Join();
        }

        private void RegenerateFile()
        {
            try
            {
                Directory.CreateDirectory(_directoryPath);
                _filePath = CreateFileName(_fileBaseName);
                _filePath = Path.Combine(_directoryPath, _filePath);
                CleanUpLogs(_directoryPath);
                _currentLogSize = 0;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void LoggingThread()
        {
            while (_writeLogEvent.WaitOne())
            {
                try
                {
                    if (_currentLogSize == -1 || _currentLogSize > 1024 * 1024 * 5)
                    {
                        RegenerateFile();
                    }

                    using (var writer = new StreamWriter(File.Open(_filePath, FileMode.Append)))
                    {
                        while (_logQueue.TryDequeue(out var log))
                        {
                            var logMsg = log.ToString();
                            _currentLogSize += logMsg.Length;
                            writer.WriteLine(logMsg);
                        }
                    }
                }
                catch
                {
                    // ignore
                }

                if (_exit)
                {
                    return;
                }
            }
        }

        private string CreateFileName(string fileBaseName)
        {
            if (string.IsNullOrWhiteSpace(fileBaseName))
            {
                fileBaseName = "log";
            }

            var currentTime = DateTime.Now.ToString(
                "yyyyMMdd-HHmmss-fff",
                System.Globalization.CultureInfo.InvariantCulture);

            return $"{fileBaseName}_{currentTime}.log";
        }

        private void CleanUpLogs(string directoryPath)
        {
            var directoryInfo = new DirectoryInfo(directoryPath);
            var fileList = directoryInfo.GetFiles("*_????????-??????-???.log")
                .Where(it => it.CreationTime <= DateTime.Now.AddDays(-5));

            foreach (var logFile in fileList)
            {
                try
                {
                    logFile.Delete();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
    }
}
