namespace RtspClientSharp.Rtcp
{
    class RtcpReportBlock
    {
        public const int Size = 24;

        public uint SyncSourceId { get; }
        public int FractionLost { get; }
        public uint CumulativePacketLost { get; }
        public uint ExtHighestSequenceNumberReceived { get; }
        public uint Jitter { get; } //not implemented, always zero
        public uint LastNtpTimeSenderReportReceived { get; }
        public uint DelaySinceLastTimeSenderReportReceived { get; }

        public RtcpReportBlock(uint syncSourceId, int fractionLost, uint cumulativePacketLost,
            uint extHighestSequenceNumberReceived, uint jitter, uint lastNtpTimeSenderReportReceived,
            uint delaySinceLastTimeSenderReportReceived)
        {
            SyncSourceId = syncSourceId;
            FractionLost = fractionLost;
            CumulativePacketLost = cumulativePacketLost;
            ExtHighestSequenceNumberReceived = extHighestSequenceNumberReceived;
            Jitter = jitter;
            LastNtpTimeSenderReportReceived = lastNtpTimeSenderReportReceived;
            DelaySinceLastTimeSenderReportReceived = delaySinceLastTimeSenderReportReceived;
        }
    }
}