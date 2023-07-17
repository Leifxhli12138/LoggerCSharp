namespace LoggerCSharp.LogUtil.Writers
{
    public interface ILogWritter
    {
        void WriteMessage(LogEntry message);

        void Stop();
    }
}
