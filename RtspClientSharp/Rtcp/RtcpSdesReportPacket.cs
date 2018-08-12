using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RtspClientSharp.Rtcp
{
    class RtcpSdesReportPacket : RtcpPacket, ISerializablePacket
    {
        private static readonly byte[] PaddingBytes = {0, 0, 0};
        private readonly int _paddingByteCount;

        public IReadOnlyList<RtcpSdesChunk> Chunks { get; }

        public RtcpSdesReportPacket(IReadOnlyList<RtcpSdesChunk> chunks)
        {
            Chunks = chunks;

            SourceCount = chunks.Count;
            PayloadType = 202;

            int length = chunks.Sum(chunk => chunk.SerializedLength);

            int fraction = length % 4;

            if (fraction == 0)
                PaddingFlag = false;
            else
            {
                PaddingFlag = true;
                _paddingByteCount = 4 - fraction;
            }

            DwordLength = (length + 3) / 4;
            Length = (DwordLength + 1) * 4;
        }

        protected override void FillFromByteSegment(ArraySegment<byte> byteSegment)
        {
        }

        public new void Serialize(Stream stream)
        {
            base.Serialize(stream);

            for (var i = 0; i < Chunks.Count; i++)
            {
                RtcpSdesChunk chunk = Chunks[i];
                chunk.Serialize(stream);
            }

            if (PaddingFlag)
                stream.Write(PaddingBytes, 0, _paddingByteCount);
        }
    }
}