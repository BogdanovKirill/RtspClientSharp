using System;
using RtspClientSharp.Codecs.Audio;
using RtspClientSharp.RawFrames.Audio;

namespace RtspClientSharp.MediaParsers
{
    class G726AudioPayloadParser : MediaPayloadParser
    {
        private readonly int _bitsPerCodedSample;

        public G726AudioPayloadParser(G726CodecInfo g726CodecInfo)
        {
            if (g726CodecInfo == null) throw new ArgumentNullException(nameof(g726CodecInfo));

            _bitsPerCodedSample = GetBitsPerCodedSample(g726CodecInfo.Bitrate);
        }

        public override void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit)
        {
            DateTime timestamp = GetFrameTimestamp(timeOffset);
            var frame = new RawG726Frame(timestamp, byteSegment, _bitsPerCodedSample);
            OnFrameGenerated(frame);
        }

        public override void ResetState()
        {
        }

        private int GetBitsPerCodedSample(int bitrate)
        {
            int bitsPerCodedSample;

            switch (bitrate)
            {
                case 16000:
                    bitsPerCodedSample = 2;
                    break;
                case 24000:
                    bitsPerCodedSample = 3;
                    break;
                case 32000:
                    bitsPerCodedSample = 4;
                    break;
                case 40000:
                    bitsPerCodedSample = 5;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bitrate));
            }

            return bitsPerCodedSample;
        }
    }
}