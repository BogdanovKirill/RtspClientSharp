using System;
using RtspClientSharp.Codecs.Audio;
using RtspClientSharp.RawFrames.Audio;

namespace RtspClientSharp.MediaParsers
{
    class G711AudioPayloadParser : MediaPayloadParser
    {
        private readonly G711CodecInfo _g711CodecInfo;

        public G711AudioPayloadParser(G711CodecInfo g711CodecInfo)
        {
            _g711CodecInfo = g711CodecInfo ?? throw new ArgumentNullException(nameof(g711CodecInfo));
        }

        public override void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit)
        {
            var g711UCodecInfo = _g711CodecInfo as G711UCodecInfo;

            RawG711Frame frame;

            DateTime timestamp = GetFrameTimestamp(timeOffset);

            if (g711UCodecInfo != null)
                frame = new RawG711UFrame(timestamp, byteSegment);
            else
                frame = new RawG711AFrame(timestamp, byteSegment);

            frame.SampleRate = _g711CodecInfo.SampleRate;
            frame.Channels = _g711CodecInfo.Channels;

            OnFrameGenerated(frame);
        }

        public override void ResetState()
        {
        }
    }
}