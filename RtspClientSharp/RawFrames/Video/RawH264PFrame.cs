using System;

namespace RtspClientSharp.RawFrames.Video
{
    public class RawH264PFrame : RawH264Frame
    {
        public RawH264PFrame(DateTime timestamp, ArraySegment<byte> frameSegment) :
            base(timestamp, frameSegment)
        {
        }
    }
}