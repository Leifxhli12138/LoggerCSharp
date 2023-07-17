using System;
using System.Diagnostics;
using LoggerCSharp.LogUtil.Writers;
using LoggerCSharp.RegistryUtil;
using Microsoft.Win32;

namespace LoggerCSharp.LogUtil
{
    // how to use this logger:
    // 1. Logger.Setup(string appName). where appName is your real AppName.
    // 2. Logger.Trace, Logger.Debug, Logger.Info, Logger.Warning, Logger.Error, Logger.Fatal.
    // 3. by default logs are turned off, you need to add reg key HKEY_LOCAL_MACHINE\SOFTWARE\WOW64\LoggerCSharp\[yourappname]-REG_SZ set to the level you like to see.
    public static class Logger
    {
        private const string LogBaseRegKey = @"SOFTWARE\LoggerCSharp";
        private static readonly LogWritterManager LoggeWritterManager = new LogWritterManager();
        private static bool _initialized;
        private static RegistryMonitor _regMonitor;
        private static readonly Type CurType = typeof(Logger);

        public delegate void WriteMessage(string message);

        public enum Level
        {
            Trace = 0, // trace log ( you can do something like entering some function)
            Debug = 1, // debug log ( very much like trace...)
            Info = 2,  // informational log (log some var that you might be intrested in)
            Warn = 3,  // warnign log(when exceptions happens, use this)
            Error = 4, // error log( unalbe to handle the exception, use error)
            Fatal = 5, // fatal log( use this when the application is crashing, like unhandled expcetion)
            None = 6,  // disable log
        }

        public static Level CurrentLevel { get; set; }

        // For FileLogger.
        public static void Setup(string fileName)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            // anything bad happened, do it in None.
            CurrentLevel = Level.None;

            ReadLogLevelFromRegistry(fileName);
            _regMonitor?.Dispose();
            _regMonitor = null;
            _regMonitor = new RegistryMonitor($@"HKLM\{LogBaseRegKey}")
            {
                MonitorFlags = MonitorFlags.Value,
            };
            _regMonitor.RegChanged += (sender, e) => { ReadLogLevelFromRegistry(fileName); };
            _regMonitor.Start();

            LoggeWritterManager.AddPublisher(new FileWriter(fileName));
        }

        public static void Setup(ILogWritter logWritter, Level logLevel = Level.Trace)
        {
            CurrentLevel = logLevel;
            if (logWritter != null)
            {
                _initialized = true;
                LoggeWritterManager.AddPublisher(logWritter);
            }
        }

        public static void Stop()
        {
        }
        
        public static void Log(Level level, string message)
        {
            if (_initialized && level >= CurrentLevel)
            {
                var stackFrame = FindStackFrame();

                Log(level, stackFrame, message);
            }
        }

        public static void LogTrace(string message) => Log(Level.Trace, message);

        public static void LogDebug(string message) => Log(Level.Debug, message);

        public static void LogInfo(string message) => Log(Level.Info, message);

        public static void LogWarn(string message) => Log(Level.Warn, message);

        public static void LogError(string message) => Log(Level.Error, message);

        public static void LogFatal(string message) => Log(Level.Fatal, message);

        public static WriteMessage Log(Level level)
        {
            if (_initialized && level >= CurrentLevel)
            {
                var stackFrame = FindStackFrame();
                return (message) =>
                {
                    Log(level, stackFrame, message);
                };
            }
            return null;
        }

        public static WriteMessage Trace()=> Log(Level.Trace);

        public static WriteMessage Debug() => Log(Level.Debug);

        public static WriteMessage Info() => Log(Level.Info);

        public static WriteMessage Warn() => Log(Level.Warn);

        private static void ReadLogLevelFromRegistry(string fileName)
        {
            try
            {
                using (var lm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key = lm.OpenSubKey(LogBaseRegKey, false) ?? lm.CreateSubKey(LogBaseRegKey))
                {
                    var defaultLevel = key?.GetValue("AllLogs")?.ToString();
                    if (Enum.TryParse(defaultLevel, out Level newLevel))
                    {
                        CurrentLevel = newLevel;
                    }

                    var levelValue = key?.GetValue(fileName)?.ToString();
                    if (Enum.TryParse(levelValue, out newLevel))
                    {
                        CurrentLevel = newLevel;
                    }
                }
            }
            catch (Exception)
            {
                // cannot do anything about it now.
            }
        }

        private static void Log(Level level, StackFrame stackFrame, string message)
        {
            try
            {
                var logEntry = new LogEntry(level, stackFrame, message);
                LoggeWritterManager.PublishLog(logEntry);
            }
            catch (Exception)
            {
                // Ignore it.
            }
        }

        private static StackFrame FindStackFrame()
        {
            try
            {
                var stackTrace = new StackTrace();

                // start from 2 as 0 is FindStackFrame(), 1 is the inner Log function, so 2 has the most chance of being the first none logger function.
                for (var i = 2; i < stackTrace.FrameCount; i++)
                {
                    var methodBase = stackTrace.GetFrame(i).GetMethod();
                    if (methodBase.ReflectedType != CurType)
                    {
                        return new StackFrame(i, false);
                    }
                }
            }
            catch (Exception)
            {
                // Ignore it.
            }

            return null;
        }
    }
}