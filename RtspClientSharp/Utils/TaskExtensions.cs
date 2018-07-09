using System.Runtime.CompilerServices;
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
    }
}