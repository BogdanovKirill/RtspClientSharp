using System;
using System.Diagnostics;
using RtspClientSharp.MediaParsers;

namespace RtspClientSharp.Utils
{
    class ElasticBuffer
    {
        private readonly int _maxPayloadSize;
        private byte[] _buffer;

        public int CountData { get; private set; }

        public ArraySegment<byte> StateByteSegment => new ArraySegment<byte>(_buffer, 0, CountData);

        public ElasticBuffer(int defaultPayloadSize, int maxPayloadSize)
        {
            if (defaultPayloadSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(defaultPayloadSize));
            if (maxPayloadSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxPayloadSize));
            if (defaultPayloadSize > maxPayloadSize)
                throw new ArgumentOutOfRangeException(nameof(defaultPayloadSize));

            _maxPayloadSize = maxPayloadSize;
            _buffer = new byte[defaultPayloadSize];
        }

        public void AddBytes(ArraySegment<byte> byteSegment)
        {
            int freeSpace = _buffer.Length - CountData;

            if (freeSpace >= byteSegment.Count)
            {
                AddBytesInternal(byteSegment);
                return;
            }

            int newSize = _buffer.Length + (byteSegment.Count - freeSpace);

            if (newSize > _maxPayloadSize)
                throw new BufferOverflowException("Buffer is reached maximum allowed size");

            int alignedNewSize = SystemMemory.RoundToPageAlignmentSize(newSize);

            Array.Resize(ref _buffer, alignedNewSize);
            AddBytesInternal(byteSegment);
        }

        public ArraySegment<byte> GetAccumulatedBytes()
        {
            var arraySegment = new ArraySegment<byte>(_buffer, 0, CountData);
            CountData = 0;
            return arraySegment;
        }

        public void ResetState()
        {
            CountData = 0;
        }

        private void AddBytesInternal(ArraySegment<byte> byteSegment)
        {
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");
            Buffer.BlockCopy(byteSegment.Array, byteSegment.Offset, _buffer, CountData, byteSegment.Count);
            CountData += byteSegment.Count;
        }
    }
}