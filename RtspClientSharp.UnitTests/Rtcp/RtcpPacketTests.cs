using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Rtcp;

namespace RtspClientSharp.UnitTests.Rtcp
{
    [TestClass]
    public class RtcpPacketTests
    {
        [TestMethod]
        public void Parse_SenderReportBytes_ReturnsValidPacket()
        {
            byte[] senderReportBytes =
            {
                0x80, 0xC8, 0x00, 0x06, 0x02, 0x75, 0x3B, 0x30,
                0xDF, 0x00, 0x01, 0x40, 0x53, 0x13, 0xAD, 0x5B, 0x00,
                0x00, 0x17, 0x70, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00,
                0x04, 0xF7
            };

            var segment = new ArraySegment<byte>(senderReportBytes);
            IEnumerable<RtcpPacket> packets = RtcpPacket.Parse(segment);

            var senderReportPacket = (RtcpSenderReportPacket) packets.First();
            Assert.AreEqual(2, senderReportPacket.ProtocolVersion);
            Assert.AreEqual(false, senderReportPacket.PaddingFlag);
            Assert.AreEqual(200, senderReportPacket.PayloadType);
            Assert.AreEqual(0, senderReportPacket.SourceCount);
            Assert.AreEqual(28, senderReportPacket.Length);
            Assert.AreEqual(0x2753B30u, senderReportPacket.SyncSourceId);
            Assert.AreEqual((long) 0xDF000140 << 32 | 0x5313AD5Bu, senderReportPacket.NtpTimestamp);
        }

        [TestMethod]
        public void Parse_RtcpByePacketBytes_ReturnsValidPacket()
        {
            byte[] byePacketBytes = {0x80, 0xCB, 0x00, 0x00};

            var segment = new ArraySegment<byte>(byePacketBytes);
            IEnumerable<RtcpPacket> packets = RtcpPacket.Parse(segment);

            var byePacket = (RtcpByePacket) packets.First();
            Assert.AreEqual(2, byePacket.ProtocolVersion);
            Assert.AreEqual(false, byePacket.PaddingFlag);
            Assert.AreEqual(203, byePacket.PayloadType);
            Assert.AreEqual(0, byePacket.SourceCount);
        }
    }
}