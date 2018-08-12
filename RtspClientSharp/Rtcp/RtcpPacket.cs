using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using RtspClientSharp.Utils;

namespace RtspClientSharp.Rtcp
{
    abstract class RtcpPacket
    {
        public int ProtocolVersion { get; protected set; } = 2;
        public bool PaddingFlag { get; protected set; }
        public int SourceCount { get; protected set; }
        public int PayloadType { get; protected set; }
        public int DwordLength { get; protected set; }
        public int Length { get; protected set; }

        protected abstract void FillFromByteSegment(ArraySegment<byte> byteSegment);


        protected void Serialize(Stream stream)
        {
            int padding = PaddingFlag ? 1 : 0;

            stream.WriteByte((byte) ((ProtocolVersion << 6) | (padding << 5) | (SourceCount & 0x1F)));
            stream.WriteByte((byte) PayloadType);
            stream.WriteByte((byte) (DwordLength >> 8));
            stream.WriteByte((byte) DwordLength);
        }

        public static IEnumerable<RtcpPacket> Parse(ArraySegment<byte> byteSegment)
        {
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");

            int offset = byteSegment.Offset;
            int totalLength = byteSegment.Count;

            while (totalLength > 0)
            {
                int value = byteSegment.Array[offset++];
                int version = value >> 6;
                int padding = (value >> 5) & 1;
                int sourceCount = value & 0x1F;

                int payloadType = byteSegment.Array[offset++];
                int dwordLength = BigEndianConverter.ReadUInt16(byteSegment.Array, offset);
                offset += 2;

                int payloadLength = dwordLength * 4;

                if (payloadLength > totalLength - 4)
                    throw new ArgumentException(
                        "Invalid RTCP packet size. It seems that data segment contains bad data", nameof(byteSegment));

                RtcpPacket packet;

                if (payloadType == 200)
                    packet = new RtcpSenderReportPacket();
                else if (payloadType == 203)
                    packet = new RtcpByePacket();
                else
                {
                    offset += payloadLength;
                    totalLength -= 4 + payloadLength;
                    continue;
                }

                packet.ProtocolVersion = version;
                packet.PaddingFlag = padding != 0;
                packet.SourceCount = sourceCount;
                packet.PayloadType = payloadType;
                packet.DwordLength = dwordLength;
                packet.Length = (dwordLength + 1) * 4;

                var segment = new ArraySegment<byte>(byteSegment.Array, offset, payloadLength);
                packet.FillFromByteSegment(segment);

                yield return packet;

                offset += payloadLength;
                totalLength -= 4 + payloadLength;
            }
        }
    }
}