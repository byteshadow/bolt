using System;

namespace BoltMQ.Core
{
    public abstract class Disposable : IDisposable
    {
        private readonly object _disposeLock = new object();

        public bool Disposed { get; private set; }

        #region Implementation of IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            lock (_disposeLock)
            {
                if (Disposed || !disposing) return;

                OnDispose();

                Disposed = true;
            }
        }

        public abstract void OnDispose();

        ~Disposable()
        {
            Dispose(!Disposed);
        }

        #endregion
    }
}
