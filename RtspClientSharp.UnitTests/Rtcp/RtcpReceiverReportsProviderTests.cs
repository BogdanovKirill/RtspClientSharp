using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using RtspClientSharp.Rtcp;
using RtspClientSharp.Rtp;

namespace RtspClientSharp.UnitTests.Rtcp
{
    [TestClass]
    public class RtcpReceiverReportsProviderTests
    {
        [TestMethod]
        public void GetReportPackets_FakeDataProviders_ResetStateOfRtpStatisticsProviderShouldBeCalled()
        {
            var rtpStatisticsProviderMock = new Mock<IRtpStatisticsProvider>();
            var rtcpSenderStatisticsProviderFake = new Mock<IRtcpSenderStatisticsProvider>();

            var rtcpReportsProvider = new RtcpReceiverReportsProvider(rtpStatisticsProviderMock.Object,
                rtcpSenderStatisticsProviderFake.Object, 1);

            rtcpReportsProvider.GetReportPackets().ToList();

            rtpStatisticsProviderMock.Verify(x => x.ResetState());
        }

        [TestMethod]
        public void GetReportPackets_TestDataProviders_ReturnsPacketWithValidReceiverReport()
        {
            DateTime timestamp = DateTime.UtcNow;

            var rtpStatisticsProviderFake = new Mock<IRtpStatisticsProvider>();
            rtpStatisticsProviderFake.Setup(x => x.CumulativePacketLost).Returns(100);
            rtpStatisticsProviderFake.Setup(x => x.HighestSequenceNumberReceived).Returns(10);
            rtpStatisticsProviderFake.Setup(x => x.PacketsReceivedSinceLastReset).Returns(10);
            rtpStatisticsProviderFake.Setup(x => x.PacketsLostSinceLastReset).Returns(4);
            rtpStatisticsProviderFake.Setup(x => x.SequenceCycles).Returns(2);
            rtpStatisticsProviderFake.Setup(x => x.SyncSourceId).Returns(99987);

            var rtcpSenderStatisticsProviderFake = new Mock<IRtcpSenderStatisticsProvider>();
            rtcpSenderStatisticsProviderFake.Setup(x => x.LastTimeReportReceived).Returns(timestamp);
            rtcpSenderStatisticsProviderFake.Setup(x => x.LastNtpTimeReportReceived).Returns(1234 << 16);

            var rtcpReportsProvider = new RtcpReceiverReportsProvider(rtpStatisticsProviderFake.Object,
                rtcpSenderStatisticsProviderFake.Object, 1112234);

            IReadOnlyList<RtcpPacket> packets = rtcpReportsProvider.GetReportPackets().ToList();

            var receiverReportPacket = (RtcpReceiverReportPacket) packets.First(p => p is RtcpReceiverReportPacket);
            Assert.IsFalse(receiverReportPacket.PaddingFlag);
            Assert.AreNotEqual(0, receiverReportPacket.SourceCount);
            Assert.AreEqual(201, receiverReportPacket.PayloadType);
            Assert.AreNotEqual(0, receiverReportPacket.DwordLength);
            Assert.AreNotEqual(0, receiverReportPacket.Length);
            Assert.AreEqual(1112234u, receiverReportPacket.SyncSourceId);
            Assert.AreEqual(99987u, receiverReportPacket.Reports[0].SyncSourceId);
            Assert.AreEqual(102, receiverReportPacket.Reports[0].FractionLost);
            Assert.AreEqual(100u, receiverReportPacket.Reports[0].CumulativePacketLost);
            Assert.AreEqual(2 << 16 | 10u, receiverReportPacket.Reports[0].ExtHighestSequenceNumberReceived);
            Assert.AreEqual(1234u, receiverReportPacket.Reports[0].LastNtpTimeSenderReportReceived);
            Assert.AreEqual(0u, receiverReportPacket.Reports[0].DelaySinceLastTimeSenderReportReceived);
        }

        [TestMethod]
        public void GetReportPackets_TestDataProviders_ReturnsPacketWithValidSdesReport()
        {
            var rtpStatisticsProviderFake = new Mock<IRtpStatisticsProvider>();
            var rtcpSenderStatisticsProviderFake = new Mock<IRtcpSenderStatisticsProvider>();

            var rtcpReportsProvider = new RtcpReceiverReportsProvider(rtpStatisticsProviderFake.Object,
                rtcpSenderStatisticsProviderFake.Object, 1112234);

            IReadOnlyList<RtcpPacket> packets = rtcpReportsProvider.GetReportPackets().ToList();

            var sdesReportPacket = (RtcpSdesReportPacket) packets.First(p => p is RtcpSdesReportPacket);

            Assert.AreEqual(1112234u, sdesReportPacket.Chunks[0].SyncSourceId);

            var nameItem = (RtcpSdesNameItem) sdesReportPacket.Chunks[0].Items.First(i => i is RtcpSdesNameItem);
            Assert.IsNotNull(nameItem.DomainName);
        }
    }
}