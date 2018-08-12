using System;
using RtspClientSharp.Utils;

namespace RtspClientSharp.Rtcp
{
    class RtcpSenderReportPacket : RtcpPacket
    {
        public uint SyncSourceId { get; private set; }
        public long NtpTimestamp { get; private set; }

        protected override void FillFromByteSegment(ArraySegment<byte> byteSegment)
        {
            SyncSourceId = BigEndianConverter.ReadUInt32(byteSegment.Array, byteSegment.Offset);

            uint ntpTimestampMw = BigEndianConverter.ReadUInt32(byteSegment.Array, byteSegment.Offset + 4);
            uint ntpTimestampLw = BigEndianConverter.ReadUInt32(byteSegment.Array, byteSegment.Offset + 8);

            NtpTimestamp = (long) ntpTimestampMw << 32 | ntpTimestampLw;
        }
    }
}