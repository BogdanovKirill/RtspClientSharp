using System;
using System.Threading;

namespace RtspClientSharp.Utils
{
    /// <summary>
    /// From CLR via C#, fourth edition
    /// </summary>
    internal sealed class SimpleHybridLock : IDisposable
    {
        // The Int32 is used by the primitive user-mode constructs (Interlocked methods)
        private int _waiters;

        // The AutoResetEvent is the primitive kernel-mode construct
        private readonly AutoResetEvent _waiterLock = new AutoResetEvent(false);

        public void Enter()
        {
            // Indicate that this thread wants the lock
            if (Interlocked.Increment(ref _waiters) == 1)
                return; // Lock was free, no contention, just return
            // Another thread is waiting. There is contention, block this thread
            _waiterLock.WaitOne(); // Bad performance hit here
            // When WaitOne returns, this thread now has the lock
        }

        public void Leave()
        {
            // This thread is releasing the lock
            if (Interlocked.Decrement(ref _waiters) == 0)
                return; // No other threads are blocked, just return
            // Other threads are blocked, wake 1 of them
            _waiterLock.Set(); // Bad performance hit here
        }

        public void Dispose()
        {
            _waiterLock.Dispose();
        }
    }
}