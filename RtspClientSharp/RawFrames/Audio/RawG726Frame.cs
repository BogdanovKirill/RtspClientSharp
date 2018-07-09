using System;

namespace RtspClientSharp.RawFrames.Audio
{
    public class RawG726Frame : RawAudioFrame
    {
        public int BitsPerCodedSample { get; }

        public RawG726Frame(DateTime timestamp, ArraySegment<byte> frameSegment, int bitsPerCodedSample)
            : base(timestamp, frameSegment)
        {
            BitsPerCodedSample = bitsPerCodedSample;
        }
    }
}