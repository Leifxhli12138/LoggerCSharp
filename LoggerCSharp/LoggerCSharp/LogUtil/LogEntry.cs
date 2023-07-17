using System;
using System.Diagnostics;
using System.Threading;

namespace LoggerCSharp.LogUtil
{
    public class LogEntry
    {
        private readonly DateTime _dateTime;
        private readonly Logger.Level _level;
        private readonly string _methodFullName;
        private readonly string _message;
        private readonly int _threadId;

        public LogEntry(Logger.Level level, StackFrame frame, string message)
        {
            _dateTime = DateTime.Now;
            _threadId = Thread.CurrentThread.ManagedThreadId;
            _level = level;
            _message = message;
            if (frame != null)
            {
                _methodFullName = $"{frame.GetMethod().ReflectedType?.Name}.{frame.GetMethod().Name}";
            }
        }

        public ConsoleColor Color
        {
            get
            {
                var color = Console.ForegroundColor;
                switch (_level)
                {
                    case Logger.Level.Trace:
                        color = ConsoleColor.White;
                        break;
                    case Logger.Level.Debug:
                        color = ConsoleColor.Gray;
                        break;
                    case Logger.Level.Info:
                        color = ConsoleColor.Cyan;
                        break;
                    case Logger.Level.Warn:
                        color = ConsoleColor.Yellow;
                        break;
                    case Logger.Level.Error:
                        color = ConsoleColor.Red;
                        break;
                    case Logger.Level.Fatal:
                        color = ConsoleColor.DarkRed;
                        break;
                }

                return color;
            }
        }

        public override string ToString()
        {
            return $"{_dateTime:yyyy-MM-dd HH:mm:ss.fff}:[tid:{_threadId}] {_level} [{_methodFullName}]: {_message}";
        }
    }
}
