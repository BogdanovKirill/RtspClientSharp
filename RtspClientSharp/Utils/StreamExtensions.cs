using System.IO;
using System.Threading.Tasks;

namespace RtspClientSharp.Utils
{
    static class StreamExtensions
    {
        public static async Task ReadExactAsync(this Stream stream, byte[] buffer, int offset, int count)
        {
            if (count == 0)
                return;

            do
            {
                int read = await stream.ReadAsync(buffer, offset, count);

                if (read == 0)
                    throw new EndOfStreamException();

                count -= read;
                offset += read;
            } while (count != 0);
        }
    }
}