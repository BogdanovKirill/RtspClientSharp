using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using RtspClientSharp.MediaParsers;
using RtspClientSharp.Rtp;

namespace RtspClientSharp.UnitTests.Rtp
{
    [TestClass]
    public class RtpStreamTests
    {
        [TestMethod]
        public void Process_TestPacket_FieldsUpdated()
        {
            var testPacketBytes = new byte[] {0x80, 0x60, 0x51, 0x50, 0x89, 0xBB, 0x82, 0xED, 0x42, 0x60, 0xD6, 0x86};
            var mediaPayloadParserMock = new Mock<IMediaPayloadParser>();

            var rtpStream = new RtpStream(mediaPayloadParserMock.Object, 1);
            rtpStream.Process(new ArraySegment<byte>(testPacketBytes));

            Assert.AreEqual(0x4260D686u, rtpStream.SyncSourceId);
            Assert.AreEqual(20816, rtpStream.HighestSequenceNumberReceived);
            Assert.AreEqual(1, rtpStream.PacketsReceivedSinceLastReset);
            Assert.AreEqual(0, rtpStream.PacketsLostSinceLastReset);
            Assert.AreEqual(0u, rtpStream.CumulativePacketLost);
            Assert.AreEqual(0, rtpStream.SequenceCycles);
        }

        [TestMethod]
        public void Process_TestPacket_ParseShouldBeCalled()
        {
            var testPacketBytes = new byte[]
                {0x80, 0x60, 0x51, 0x50, 0x89, 0xBB, 0x82, 0xED, 0x42, 0x60, 0xD6, 0x86, 0x01};
            var mediaPayloadParserMock = new Mock<IMediaPayloadParser>();

            var rtpStream = new RtpStream(mediaPayloadParserMock.Object, 1);
            rtpStream.Process(new ArraySegment<byte>(testPacketBytes));

            mediaPayloadParserMock.Verify(x => x.Parse(It.IsAny<TimeSpan>(),
                It.Is<ArraySegment<byte>>(p => p.Count == 1),
                It.Is<bool>(m => m == false)));
        }

        [TestMethod]
        public void Process_TwoTestPacketsWithLargeSequenceDifference_ResetStateShouldBeCalled()
        {
            var testPacketBytes1 = new byte[] {0x80, 0x60, 0x00, 0x00, 0x89, 0xBB, 0x82, 0xED, 0x42, 0x60, 0xD6, 0x86};
            var testPacketBytes2 = new byte[] {0x80, 0x60, 0x51, 0x50, 0x89, 0xBB, 0x82, 0xED, 0x42, 0x60, 0xD6, 0x86};
            var mediaPayloadParserMock = new Mock<IMediaPayloadParser>();

            var rtpStream = new RtpStream(mediaPayloadParserMock.Object, 1);
            rtpStream.Process(new ArraySegment<byte>(testPacketBytes1));
            rtpStream.Process(new ArraySegment<byte>(testPacketBytes2));

            mediaPayloadParserMock.Verify(x => x.ResetState());
        }
    }
}