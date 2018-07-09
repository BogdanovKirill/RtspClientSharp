using System;
using System.Linq;

namespace RtspClientSharp.Utils
{
    static class Hex
    {
        public static byte[] StringToByteArray(string hex)
        {
            if (hex == null)
                throw new ArgumentNullException(nameof(hex));

            if (hex.Length == 0)
                return Array.Empty<byte>();

            return Enumerable.Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        }
    }
}