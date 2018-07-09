using System;

namespace RtspClientSharp.RawFrames
{
    public abstract class RawFrame
    {
        public DateTime Timestamp { get; }
        public ArraySegment<byte> FrameSegment { get; }
        public abstract FrameType Type { get; }

        protected RawFrame(DateTime timestamp, ArraySegment<byte> frameSegment)
        {
            Timestamp = timestamp;
            FrameSegment = frameSegment;
        }
    }
}