using System;
using System.Collections.Generic;
using RtspClientSharp.Rtp;

namespace RtspClientSharp.Rtcp
{
    class RtcpReceiverReportsProvider
    {
        private readonly IRtpStatisticsProvider _rtpStatisticsProvider;
        private readonly IRtcpSenderStatisticsProvider _rtcpSenderStatisticsProvider;
        private readonly uint _senderSyncSourceId;
        private readonly List<RtcpPacket> _reportPacketsList = new List<RtcpPacket>();

        public RtcpReceiverReportsProvider(IRtpStatisticsProvider rtpStatisticsProvider,
            IRtcpSenderStatisticsProvider rtcpSenderStatisticsProvider, uint senderSyncSourceId)
        {
            _rtpStatisticsProvider = rtpStatisticsProvider ??
                                     throw new ArgumentNullException(nameof(rtpStatisticsProvider));
            _rtcpSenderStatisticsProvider = rtcpSenderStatisticsProvider ??
                                            throw new ArgumentNullException(nameof(rtcpSenderStatisticsProvider));

            _senderSyncSourceId = senderSyncSourceId;
        }

        public IReadOnlyList<RtcpPacket> GetReportPackets()
        {
            _reportPacketsList.Clear();

            RtcpReceiverReportPacket receiverReport = CreateReceiverReport();
            _reportPacketsList.Add(receiverReport);
            RtcpSdesReportPacket sdesReport = CreateSdesReport();
            _reportPacketsList.Add(sdesReport);

            return _reportPacketsList;
        }

        private RtcpReceiverReportPacket CreateReceiverReport()
        {
            int fractionLost;

            if (_rtpStatisticsProvider.PacketsReceivedSinceLastReset == 0)
                fractionLost = 0;
            else
                fractionLost = _rtpStatisticsProvider.PacketsLostSinceLastReset * 256 /
                               _rtpStatisticsProvider.PacketsReceivedSinceLastReset;

            uint lastNtpTimeSenderReportReceived;
            uint delaySinceLastTimeSenderReportReceived;

            if (_rtcpSenderStatisticsProvider.LastTimeReportReceived == DateTime.MinValue)
            {
                lastNtpTimeSenderReportReceived = 0;
                delaySinceLastTimeSenderReportReceived = 0;
            }
            else
            {
                lastNtpTimeSenderReportReceived =
                    (uint) ((_rtcpSenderStatisticsProvider.LastNtpTimeReportReceived >> 16) & uint.MaxValue);

                TimeSpan delta = DateTime.UtcNow - _rtcpSenderStatisticsProvider.LastTimeReportReceived;
                delaySinceLastTimeSenderReportReceived = (uint) delta.TotalSeconds * 65536;
            }

            uint extHighestSequenceNumberReceived = (uint) (_rtpStatisticsProvider.SequenceCycles << 16 |
                                                            _rtpStatisticsProvider.HighestSequenceNumberReceived);

            var rtcpReportBlock = new RtcpReportBlock(_rtpStatisticsProvider.SyncSourceId, fractionLost,
                _rtpStatisticsProvider.CumulativePacketLost, extHighestSequenceNumberReceived, 0,
                lastNtpTimeSenderReportReceived, delaySinceLastTimeSenderReportReceived);

            var receiverReport = new RtcpReceiverReportPacket(_senderSyncSourceId, new[] {rtcpReportBlock});

            _rtpStatisticsProvider.ResetState();

            return receiverReport;
        }

        private RtcpSdesReportPacket CreateSdesReport()
        {
            string machineName = Environment.MachineName;

            var items = new[] {new RtcpSdesNameItem(machineName)};
            var chunks = new[] {new RtcpSdesChunk(_senderSyncSourceId, items)};

            var sdesReport = new RtcpSdesReportPacket(chunks);
            return sdesReport;
        }
    }
}