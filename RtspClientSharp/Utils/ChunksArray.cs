using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RtspClientSharp.Utils
{
    /// <summary>
    /// The main idea of that class is to reduce the amount of references to arrays for GC
    /// </summary>
    class ChunksArray
    {
        private readonly int _maxChunkSize;
        private readonly int _maxNumberOfChunks;
        private byte[] _chunksBytes = Array.Empty<byte>();
        private readonly List<int> _sizesList;
        private int _chunksCount;

        public int Count => _chunksCount;

        public ChunksArray(int maxChunkSize, int maxNumberOfChunks)
        {
            _maxChunkSize = maxChunkSize;
            _maxNumberOfChunks = maxNumberOfChunks;
            _sizesList = new List<int>(maxNumberOfChunks);
        }

        public ArraySegment<byte> this[int index]
        {
            get
            {
                int offset = index * _maxChunkSize;
                return new ArraySegment<byte>(_chunksBytes, offset, _sizesList[index]);
            }
        }

        public void Add(ArraySegment<byte> chunkSegment)
        {
            Debug.Assert(chunkSegment.Array != null, "chunkSegment.Array != null");

            if (chunkSegment.Count > _maxChunkSize)
                throw new ArgumentException($"Chunk size is too large: {chunkSegment.Count}", nameof(chunkSegment));
            if (_chunksCount == _maxNumberOfChunks)
                throw new InvalidOperationException("Number of chunks is reached the upper limit");

            int requiredSize = ++_chunksCount * _maxChunkSize;

            if (_chunksBytes.Length < requiredSize)
                Array.Resize(ref _chunksBytes, requiredSize);

            int offset = requiredSize - _maxChunkSize;
            Buffer.BlockCopy(chunkSegment.Array, chunkSegment.Offset, _chunksBytes, offset, chunkSegment.Count);

            _sizesList.Add(chunkSegment.Count);
        }

        public void RemoveAt(int index)
        {
            _sizesList.RemoveAt(index);
            --_chunksCount;

            if (index < _chunksCount)
            {
                int dstOffset = index * _maxChunkSize;
                int srcOffset = dstOffset + _maxChunkSize;
                int copyCount = _chunksCount * _maxChunkSize - dstOffset;

                Buffer.BlockCopy(_chunksBytes, srcOffset, _chunksBytes, dstOffset, copyCount);
            }
        }

        public void Clear()
        {
            _sizesList.Clear();
            _chunksCount = 0;
        }
    }
}