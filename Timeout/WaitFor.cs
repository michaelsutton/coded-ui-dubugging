// based on: https://github.com/Lokad/lokad-data-platform/blob/master/Platform.TestClient/WaitFor.cs
// this version was modified to fit my need for a reusable object, and was customized for this current issue,
// a coded ui long-running property call.

// the method used in this class for aborting the long-running thread is 
// usallly unsafe and should be avoided. but, in this particular case its 
// the only workaround. since the Coded UI platform runs on the COM STA mode.
using System;
using System.Threading;

namespace CodedUI.DebuggingHelpers.Timeout
{
    /// <summary>
    /// Helper class for invoking tasks with timeout. Overhead is 0,005 ms.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    public sealed class WaitFor<TResult>
    {
        private readonly IAbortService _abortService;
        private readonly TimeSpan _timeout;

        /// <summary>
        /// Initializes a new instance of the <see cref="WaitFor{T}"/> class, 
        /// using the specified timeout for all operations.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        public WaitFor(TimeSpan timeout)
        {
            _timeout = timeout;
            _abortService = new SimpleAbortService();
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

            try
            {
                _abortService.RequestAbort(_timeout);
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
                _abortService.Cancel();
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
