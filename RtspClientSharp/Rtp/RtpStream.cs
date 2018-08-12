using System;
using RtspClientSharp.MediaParsers;

namespace RtspClientSharp.Rtp
{
    class RtpStream : ITransportStream, IRtpStatisticsProvider
    {
        private readonly IRtpSequenceAssembler _rtpSequenceAssembler;
        private readonly IMediaPayloadParser _mediaPayloadParser;
        private readonly int _samplesFrequency;

        private ulong _samplesSum;
        private ushort _previousSeqNumber;
        private uint _previousTimestamp;
        private bool _isFirstPacket = true;

        public uint SyncSourceId { get; private set; }
        public ushort HighestSequenceNumberReceived { get; private set; }
        public int PacketsReceivedSinceLastReset { get; private set; }
        public int PacketsLostSinceLastReset { get; private set; }
        public uint CumulativePacketLost { get; private set; }
        public ushort SequenceCycles { get; private set; }

        public RtpStream(IMediaPayloadParser mediaPayloadParser, int samplesFrequency,
            IRtpSequenceAssembler rtpSequenceAssembler = null)
        {
            _mediaPayloadParser = mediaPayloadParser ?? throw new ArgumentNullException(nameof(mediaPayloadParser));
            _samplesFrequency = samplesFrequency;

            if (rtpSequenceAssembler != null)
            {
                _rtpSequenceAssembler = rtpSequenceAssembler;
                _rtpSequenceAssembler.PacketPassed += ProcessImmediately;
            }
        }

        public void Process(ArraySegment<byte> payloadSegment)
        {
            if (!RtpPacket.TryParse(payloadSegment, out RtpPacket rtpPacket))
                return;

            if (_rtpSequenceAssembler != null)
                _rtpSequenceAssembler.ProcessPacket(ref rtpPacket);
            else
                ProcessImmediately(ref rtpPacket);
        }

        private void ProcessImmediately(ref RtpPacket rtpPacket)
        {
            SyncSourceId = rtpPacket.SyncSourceId;

            if (!_isFirstPacket)
            {
                int delta = (ushort) (rtpPacket.SeqNumber - _previousSeqNumber);

                if (delta != 1)
                {
                    int lostCount = delta - 1;

                    if (lostCount == -1)
                        lostCount = ushort.MaxValue;

                    CumulativePacketLost += (uint) lostCount;

                    if (CumulativePacketLost > 0x7FFFFF)
                        CumulativePacketLost = 0x7FFFFF;

                    PacketsLostSinceLastReset += lostCount;

                    _mediaPayloadParser.ResetState();
                }

                if (rtpPacket.SeqNumber < HighestSequenceNumberReceived)
                    ++SequenceCycles;

                _samplesSum += rtpPacket.Timestamp - _previousTimestamp;
            }

            HighestSequenceNumberReceived = rtpPacket.SeqNumber;

            _isFirstPacket = false;
            ++PacketsReceivedSinceLastReset;
            _previousSeqNumber = rtpPacket.SeqNumber;
            _previousTimestamp = rtpPacket.Timestamp;

            if (rtpPacket.PayloadSegment.Count == 0)
                return;

            TimeSpan timeOffset = _samplesFrequency != 0
                ? new TimeSpan((long) (_samplesSum * 1000 / (uint) _samplesFrequency * TimeSpan.TicksPerMillisecond))
                : TimeSpan.MinValue;

            _mediaPayloadParser.Parse(timeOffset, rtpPacket.PayloadSegment, rtpPacket.MarkerBit);
        }

        public void ResetState()
        {
            PacketsLostSinceLastReset = 0;
            PacketsReceivedSinceLastReset = 0;
        }
    }
}