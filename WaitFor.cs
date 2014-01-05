using System;
using System.Threading;

// based on: https://github.com/Lokad/lokad-data-platform/blob/master/Platform.TestClient/WaitFor.cs
// this version was modified to fit my need for a reusable object, and was customized for this current issue,
// a coded ui long-running property call.

// the method used in this class for aborting the long-running thread is 
// usallly unsafe and should be avoided. but, in this particular case its 
// the only workaround. since the Coded UI platform runs on the COM STA mode.

namespace CodedUI.DebuggingHelpers
{
    /// <summary>
    /// Helper class for invoking tasks with timeout. Overhead is 0,005 ms.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    public sealed class WaitFor<TResult>
    {
        readonly TimeSpan _timeout;
        Thread _watcher;
        readonly object _sync;
        bool _completed;

        /// <summary>
        /// Initializes a new instance of the <see cref="WaitFor{T}"/> class, 
        /// using the specified timeout for all operations.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        public WaitFor(TimeSpan timeout)
        {
            _timeout = timeout;
            _sync = new object();
            _completed = false;
            _watcher = new Thread(Watch);
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
            // CAUTION: the call to Abort() can be blocking in rare situations
            // http://msdn.microsoft.com/en-us/library/ty8d3wta.aspx
            // Hence, it should not be called with the 'lock' as it could deadlock
            // with the 'finally' block in the Run method.

            if (!_completed)
            {
                watchedThread.Abort();
            }  
        }

        /// <summary>
        /// Executes the spcified function within the current thread, aborting it
        /// if it does not complete within the specified timeout interval. 
        /// </summary>
        /// <param name="function">The function.</param>
        /// <returns>result of the function</returns>
        /// <remarks>
        /// The performance trick is that we do not interrupt the current
        /// running thread. Instead, we just create a watcher that will sleep
        /// until the originating thread terminates or until the timeout is
        /// elapsed.
        /// </remarks>
        /// <exception cref="ArgumentNullException">if function is null</exception>
        /// <exception cref="TimeoutException">if the function does not finish in time </exception>
        public TResult Run(Func<TResult> function)
        {
            if (function == null) throw new ArgumentNullException("function");
            _completed = false;

            try
            {
                // using the ThreadPool makes much more sense. but currently causes a 
                // rare bug while debugging 
                //
                // ThreadPool.QueueUserWorkItem(Watch, Thread.CurrentThread);

                // TODO: avoid running a new thread every call to Run
                _watcher = new Thread(Watch);
                _watcher.Start(Thread.CurrentThread);
                return function();
            }
            catch (ThreadAbortException)
            {
                // This is our own exception.
                Thread.ResetAbort();

                throw new TimeoutException(string.Format("The operation has timed out after {0}.", _timeout));
            }
            finally
            {
                lock (_sync)
                {
                    _completed = true;
                    Monitor.Pulse(_sync);
                }
            }
        }

        /// <summary>
        /// Executes the spcified function within the current thread, aborting it
        /// if it does not complete within the specified timeout interval.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        /// <param name="function">The function.</param>
        /// <returns>result of the function</returns>
        /// <remarks>
        /// The performance trick is that we do not interrupt the current
        /// running thread. Instead, we just create a watcher that will sleep
        /// until the originating thread terminates or until the timeout is
        /// elapsed.
        /// </remarks>
        /// <exception cref="ArgumentNullException">if function is null</exception>
        /// <exception cref="TimeoutException">if the function does not finish in time </exception>
        public static TResult Run(TimeSpan timeout, Func<TResult> function)
        {
            return new WaitFor<TResult>(timeout).Run(function);
        }
    }
}
