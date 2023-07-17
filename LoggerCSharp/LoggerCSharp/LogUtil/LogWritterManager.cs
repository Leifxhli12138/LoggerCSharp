using System.Collections.Generic;
using LoggerCSharp.LogUtil.Writers;

namespace LoggerCSharp.LogUtil
{
    internal class LogWritterManager
    {
        private readonly List<ILogWritter> _logWriters = new List<ILogWritter>();

        public LogWritterManager AddPublisher(ILogWritter publisher)
        {
            if (publisher != null && !_logWriters.Contains(publisher))
            {
                _logWriters.Add(publisher);
            }

            return this;
        }

        public LogWritterManager RemovePublisher(ILogWritter publisher)
        {
            _logWriters.Remove(publisher);
            return this;
        }

        public void PublishLog(LogEntry message)
        {
            foreach (var logWritter in _logWriters)
            {
                logWritter.WriteMessage(message);
            }
        }

        public void Stop()
        {
            foreach (var logWriter in _logWriters)
            {
                logWriter.Stop();
            }
        }
    }
}
