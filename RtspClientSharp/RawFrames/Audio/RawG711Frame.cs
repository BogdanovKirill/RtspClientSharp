using System;

namespace RtspClientSharp.RawFrames.Audio
{
    public abstract class RawG711Frame : RawAudioFrame
    {
        public int SampleRate { get; set; } = 8000;
        public int Channels { get; set; } = 1;

        protected RawG711Frame(DateTime timestamp, ArraySegment<byte> frameSegment)
            : base(timestamp, frameSegment)
        {
        }
    }
}