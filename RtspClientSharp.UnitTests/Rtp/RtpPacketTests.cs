using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Rtp;

namespace RtspClientSharp.UnitTests.Rtp
{
    [TestClass]
    public class RtpPacketTests
    {
        [TestMethod]
        public void TryParse_TestBytes_ReturnsPacket()
        {
            var testBytes = new byte[] {0x80, 0x60, 0x51, 0x50, 0x89, 0xBB, 0x82, 0xED, 0x42, 0x60, 0xD6, 0x86};
            var testSegment = new ArraySegment<byte>(testBytes);

            bool parseResult = RtpPacket.TryParse(testSegment, out RtpPacket rtpPacket);

            Assert.IsTrue(parseResult);
            Assert.AreEqual(RtpPacket.RtpProtocolVersion, rtpPacket.ProtocolVersion);
            Assert.IsFalse(rtpPacket.PaddingFlag);
            Assert.IsFalse(rtpPacket.ExtensionFlag);
            Assert.AreEqual(0, rtpPacket.CsrcCount);
            Assert.IsFalse(rtpPacket.MarkerBit);
            Assert.AreEqual(96, rtpPacket.PayloadType);
            Assert.AreEqual(20816, rtpPacket.SeqNumber);
            Assert.AreEqual(0x89BB82EDu, rtpPacket.Timestamp);
            Assert.AreEqual(0x4260D686u, rtpPacket.SyncSourceId);
        }
    }
}