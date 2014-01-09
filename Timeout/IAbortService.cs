using System;

namespace CodedUI.DebuggingHelpers.Timeout
{
    public interface IAbortService
    {
        void RequestAbort(TimeSpan timeout);
        void Cancel();
    }
}
