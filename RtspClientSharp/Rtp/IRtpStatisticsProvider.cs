namespace RtspClientSharp.Rtp
{
    interface IRtpStatisticsProvider
    {
        uint SyncSourceId { get; }

        ushort HighestSequenceNumberReceived { get; }

        int PacketsReceivedSinceLastReset { get; }

        int PacketsLostSinceLastReset { get; }

        uint CumulativePacketLost { get; }

        ushort SequenceCycles { get; }

        void ResetState();
    }
}