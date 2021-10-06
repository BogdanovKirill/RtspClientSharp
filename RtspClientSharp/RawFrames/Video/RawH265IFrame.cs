using System;

namespace RtspClientSharp.RawFrames.Video
{
    public class RawH265IFrame : RawH265Frame
    {
        public ArraySegment<byte> ParametersBytesSegment { get; }

        public RawH265IFrame(DateTime timestamp, ArraySegment<byte> frameSegment, ArraySegment<byte> parametersBytesSegment)
            : base(timestamp, frameSegment)
        {
            ParametersBytesSegment = parametersBytesSegment;
        }
    }
}
