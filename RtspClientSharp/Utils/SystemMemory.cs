namespace RtspClientSharp.Utils
{
    static class SystemMemory
    {
        private const int SystemPageSize = 4096;

        public static int RoundToPageAlignmentSize(int size)
        {
            return (size + SystemPageSize - 1) / SystemPageSize * SystemPageSize;
        }
    }
}