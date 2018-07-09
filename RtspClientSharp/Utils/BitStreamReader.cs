using System;

namespace RtspClientSharp.Utils
{
    class BitStreamReader
    {
        private byte[] _buffer;
        private int _count;
        private int _startOffset;
        private int _bitsPosition;

        public void ReInitialize(ArraySegment<byte> byteSegment)
        {
            _buffer = byteSegment.Array;
            _startOffset = byteSegment.Offset;
            _bitsPosition = 0;
            _count = byteSegment.Count;
        }

        public int ReadBit()
        {
            int bytePos = _bitsPosition / 8;

            if (bytePos == _count)
                return -1;

            int posInByte = 7 - _bitsPosition % 8;
            int bit = (_buffer[_startOffset + bytePos] >> posInByte) & 1;
            ++_bitsPosition;
            return bit;
        }

        public int ReadBits(int count)
        {
            if (count > 32)
                throw new ArgumentOutOfRangeException(nameof(count));

            int res = 0;
            while (count > 0)
            {
                res = res << 1;
                int u1 = ReadBit();

                if (u1 == -1)
                    return u1;

                res |= u1;
                count--;
            }

            return res;
        }

        public int ReadUe()
        {
            int leadingZeroBits = 0;

            while (leadingZeroBits < 31)
            {
                int bit = ReadBit();

                if (bit == -1)
                    return bit;

                if (bit != 0)
                    break;

                ++leadingZeroBits;
            }

            if (leadingZeroBits > 0)
            {
                long val = ReadBits(leadingZeroBits);

                if (val == -1)
                    return -1;

                return (int) ((1 << leadingZeroBits) - 1 + val);
            }

            return 0;
        }
    }
}