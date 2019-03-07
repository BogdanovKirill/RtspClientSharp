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
        private readonly Stack<int> _freeChunks;
        private int _chunksCount;

        public int Count => _chunksCount;

        public ChunksArray(int maxChunkSize, int maxNumberOfChunks)
        {
            _maxChunkSize = maxChunkSize;
            _maxNumberOfChunks = maxNumberOfChunks;
            _sizesList = new List<int>(maxNumberOfChunks);
            _freeChunks = new Stack<int>(maxNumberOfChunks);
        }

        public ArraySegment<byte> this[int index]
        {
            get
            {
                int offset = index * _maxChunkSize;
                return new ArraySegment<byte>(_chunksBytes, offset, _sizesList[index]);
            }
        }

        public int Insert(ArraySegment<byte> chunkSegment)
        {
            Debug.Assert(chunkSegment.Array != null, "chunkSegment.Array != null");

            if (chunkSegment.Count > _maxChunkSize)
                throw new ArgumentException($"Chunk size is too large: {chunkSegment.Count}", nameof(chunkSegment));
            if (_chunksCount == _maxNumberOfChunks)
                throw new InvalidOperationException("Number of chunks is reached the upper limit");

            int index;
            int offset;

            if (_freeChunks.Count != 0)
            {
                index = _freeChunks.Pop();
                offset = index * _maxChunkSize;

                _sizesList[index] = chunkSegment.Count;
                ++_chunksCount;
            }
            else
            {
                index = _chunksCount;

                int requiredSize = ++_chunksCount * _maxChunkSize;

                if (_chunksBytes.Length < requiredSize)
                    Array.Resize(ref _chunksBytes, requiredSize);

                offset = requiredSize - _maxChunkSize;

                _sizesList.Add(chunkSegment.Count);
            }

            Buffer.BlockCopy(chunkSegment.Array, chunkSegment.Offset, _chunksBytes, offset, chunkSegment.Count);
            return index;
        }

        public void RemoveAt(int index)
        {
            --_chunksCount;

            _freeChunks.Push(index);
            _sizesList[index] = 0;
        }

        public void Clear()
        {
            _freeChunks.Clear();
            _sizesList.Clear();
            _chunksCount = 0;
        }
    }
}