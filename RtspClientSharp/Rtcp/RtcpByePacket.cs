using System;
using System.Collections.Generic;
using System.IO;
using RtspClientSharp.Rtsp;
using RtspClientSharp.Utils;

namespace RtspClientSharp.Rtcp
{
    class RtcpByePacket : RtcpPacket, ISerializablePacket
    {
        private readonly List<uint> _syncSourcesIds = new List<uint>();

        public IEnumerable<uint> SyncSourcesIds => _syncSourcesIds;

        public RtcpByePacket()
        {
            PaddingFlag = false;
            PayloadType = 203;
        }

        public RtcpByePacket(uint syncSourceId): this()
        {
            _syncSourcesIds.Add(syncSourceId);
            SourceCount = 1;
            DwordLength = 1;
            Length = (DwordLength + 1) * 4;
        }

        protected override void FillFromByteSegment(ArraySegment<byte> byteSegment)
        {
            int offset = byteSegment.Offset;

            for (int i = 0; i < SourceCount; i++)
            {
                uint ssrc = BigEndianConverter.ReadUInt32(byteSegment.Array, offset);

                offset += 4;

                _syncSourcesIds.Add(ssrc);
            }
        }

        public new void Serialize(Stream stream)
        {
            base.Serialize(stream);

            if (_syncSourcesIds.Count > 0)
            {
                stream.WriteByte((byte)(_syncSourcesIds[0] >> 24));
                stream.WriteByte((byte)(_syncSourcesIds[0] >> 16));
                stream.WriteByte((byte)(_syncSourcesIds[0] >> 8));
                stream.WriteByte((byte)_syncSourcesIds[0]);
            }
            else
                throw new RtspClientException("Can't make RTCP packet without Identifier");
        }
    }
}