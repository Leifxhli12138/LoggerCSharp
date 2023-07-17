using System;

namespace LoggerCSharp
{
    public class DisposeAction : IDisposable
    {
        private readonly Action _action = null;
        private bool _disposed = false;

        public DisposeAction(Action action)
        {
            _action = action;
        }

        #region IDisposable Support

        // To detect redundant calls
        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _action?.Invoke();
                }

                _disposed = true;
            }
        }
        #endregion

    }
}