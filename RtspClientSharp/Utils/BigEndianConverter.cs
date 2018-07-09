namespace RtspClientSharp.Utils
{
    static class BigEndianConverter
    {
        public static uint ReadUInt32(byte[] buffer, int offset)
        {
            return (uint) (buffer[offset] << 24 |
                           buffer[offset + 1] << 16 |
                           buffer[offset + 2] << 8 |
                           buffer[offset + 3]);
        }

        public static int ReadUInt24(byte[] buffer, int offset)
        {
            return buffer[offset] << 16 |
                   buffer[offset + 1] << 8 |
                   buffer[offset + 2];
        }

        public static int ReadUInt16(byte[] buffer, int offset)
        {
            return (buffer[offset] << 8) | buffer[offset + 1];
        }
    }
}