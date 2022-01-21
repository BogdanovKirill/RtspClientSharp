using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace RtspClientSharp.Utils
{
    static class TaskExtensions
    {
        public static void IgnoreExceptions(this Task task)
        {
            task.ContinueWith(HandleException,
                TaskContinuationOptions.OnlyOnFaulted |
                TaskContinuationOptions.ExecuteSynchronously);
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static void HandleException(Task task)
        {
            var ignore = task.Exception;
        }

        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
            {
                if (task != await Task.WhenAny(task, tcs.Task))
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            return task.Result;
        }
    }
}