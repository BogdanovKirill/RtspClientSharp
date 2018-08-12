using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using RtspClientSharp.Utils;

namespace RtspClientSharp.Tpkt
{
    class TpktStream
    {
        private static readonly byte[] TpktHeaderIdArray = {TpktHeader.Id};

        private byte[] _readBuffer = new byte[8 * 1024];
        private byte[] _writeBuffer = new byte[0];

        private int _nonParsedDataSize;
        private int _nonParsedDataOffset;

        private readonly Stream _stream;

        public TpktStream(Stream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public async Task<TpktPayload> ReadAsync()
        {
            int nextTpktPositon = await FindNextPacketAsync();

            int usefulDataSize = _nonParsedDataSize - nextTpktPositon;

            if (nextTpktPositon != 0)
                Buffer.BlockCopy(_readBuffer, nextTpktPositon, _readBuffer, 0, usefulDataSize);

            int readCount = TpktHeader.Size - usefulDataSize;

            if (readCount > 0)
            {
                await _stream.ReadExactAsync(_readBuffer, usefulDataSize, readCount);
                usefulDataSize = 0;
            }
            else
                usefulDataSize = -readCount;

            int channel = _readBuffer[1];
            int payloadSize = BigEndianConverter.ReadUInt16(_readBuffer, 2);
            int totalSize = TpktHeader.Size + payloadSize;

            if (_readBuffer.Length < totalSize)
            {
                int alignedTotalSize = SystemMemory.RoundToPageAlignmentSize(totalSize);
                Array.Resize(ref _readBuffer, alignedTotalSize);
            }

            readCount = payloadSize - usefulDataSize;

            if (readCount > 0)
            {
                await _stream.ReadExactAsync(_readBuffer, TpktHeader.Size + usefulDataSize, readCount);
                _nonParsedDataSize = 0;
            }
            else
            {
                _nonParsedDataSize = -readCount;
                _nonParsedDataOffset = totalSize;
            }

            var payloadSegment = new ArraySegment<byte>(_readBuffer, TpktHeader.Size, payloadSize);
            return new TpktPayload(channel, payloadSegment);
        }

        public async Task WriteAsync(int channel, ArraySegment<byte> payloadSegment)
        {
            Debug.Assert(payloadSegment.Array != null, "payloadSegment.Array != null");

            int packetSize = TpktHeader.Size + payloadSegment.Count;

            if (_writeBuffer.Length < packetSize)
            {
                _writeBuffer = new byte[packetSize];
                _writeBuffer[0] = TpktHeader.Id;
            }

            _writeBuffer[1] = (byte) channel;
            _writeBuffer[2] = (byte) (payloadSegment.Count >> 8);
            _writeBuffer[3] = (byte) payloadSegment.Count;

            Buffer.BlockCopy(payloadSegment.Array, payloadSegment.Offset, _writeBuffer, TpktHeader.Size,
                payloadSegment.Count);

            await _stream.WriteAsync(_writeBuffer, 0, packetSize);
        }

        private async Task<int> FindNextPacketAsync()
        {
            if (_nonParsedDataSize != 0)
                Buffer.BlockCopy(_readBuffer, _nonParsedDataOffset, _readBuffer, 0, _nonParsedDataSize);

            int packetPosition;
            while ((packetPosition = FindTpktSignature(_nonParsedDataSize)) == -1)
            {
                _nonParsedDataSize = await _stream.ReadAsync(_readBuffer, 0, _readBuffer.Length);

                if (_nonParsedDataSize == 0)
                    throw new EndOfStreamException("End of TPKT stream");
            }

            return packetPosition;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindTpktSignature(int dataSize)
        {
            if (dataSize == 0)
                return -1;

            if (_readBuffer[0] == TpktHeader.Id)
                return 0;

            if (dataSize == 1)
                return -1;

            return ArrayUtils.IndexOfBytes(_readBuffer, TpktHeaderIdArray, 1, --dataSize);
        }
    }
}