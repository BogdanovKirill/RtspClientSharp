namespace RtspClientSharp.Utils
{
    static class ArrayUtils
    {
        public static bool IsBytesEquals(byte[] bytes1, int offset1, int count1, byte[] bytes2, int offset2, int count2)
        {
            if (count1 != count2)
                return false;

            for (int i = 0; i < count1; i++)
                if (bytes1[offset1 + i] != bytes2[offset2 + i])
                    return false;

            return true;
        }

        public static bool StartsWith(byte[] array, int offset, int count, byte[] pattern)
        {
            if (count < pattern.Length)
                return false;

            for (int i = 0; i < pattern.Length; i++, offset++)
            {
                if (array[offset] != pattern[i])
                    return false;
            }

            return true;
        }

        public static bool EndsWith(byte[] array, int offset, int count, byte[] pattern)
        {
            if (count < pattern.Length)
                return false;

            offset = offset + count - pattern.Length;

            for (int i = 0; i < pattern.Length; i++, offset++)
            {
                if (array[offset] != pattern[i])
                    return false;
            }

            return true;
        }

        public static int IndexOfBytes(byte[] array, byte[] pattern, int startIndex, int count)
        {
            if (count < pattern.Length)
                return -1;

            int endIndex = startIndex + count;

            int foundIndex = 0;
            for (; startIndex < endIndex; startIndex++)
            {
                if (array[startIndex] != pattern[foundIndex])
                    foundIndex = 0;
                else if (++foundIndex == pattern.Length)
                    return startIndex - foundIndex + 1;
            }

            return -1;
        }

        public static int LastIndexOfBytes(byte[] array, byte[] pattern, int startIndex, int count)
        {
            if (count < pattern.Length)
                return -1;

            int endIndex = startIndex + count - 1;

            int lastPatternIndex = pattern.Length - 1;
            int foundIndex = lastPatternIndex;
            for (; endIndex >= startIndex; endIndex--)
            {
                if (array[endIndex] != pattern[foundIndex])
                    foundIndex = lastPatternIndex;
                else if (--foundIndex == -1)
                    return endIndex;
            }

            return -1;
        }
    }
}