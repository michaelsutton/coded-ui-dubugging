using System;
using System.Threading;

namespace CodedUI.DebuggingHelpers.Timeout
{
    public class SimpleAbortService : IAbortService
    {
        private TimeSpan _timeout;
        private bool _completed;
        private Thread _watcher;

        private readonly object _sync;

        public SimpleAbortService()
        {
            _sync = new object();
            _completed = false;
            _watcher = new Thread(Watch);
        }

        public void RequestAbort(TimeSpan timeout)
        {
            _timeout = timeout;
            _completed = false;
            _watcher = new Thread(Watch);
            _watcher.Start(Thread.CurrentThread);
        }

        public void Cancel()
        {
            lock (_sync)
            {
                _completed = true;
                Monitor.Pulse(_sync);
            }
        }

        private void Watch(object obj)
        {
            var watchedThread = obj as Thread;
            if (watchedThread == null) return;

            lock (_sync)
            {
                if (!_completed)
                {
                    Monitor.Wait(_sync, _timeout);
                }
            }

            if (!_completed)
            {
                watchedThread.Abort();
            }
        }
    }
}
