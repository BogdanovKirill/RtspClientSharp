using System;
using System.Collections.Generic;
using System.Text;

namespace RtspClientSharp.RawFrames.Video
{
    public abstract class RawH265Frame : RawVideoFrame
    {
        public static readonly byte[] StartMarker = { 0, 0, 0, 1 };

        public static readonly int StartMarkerSize = StartMarker.Length;

        protected RawH265Frame(DateTime timestamp, ArraySegment<byte> frameSegment)
            : base(timestamp, frameSegment)
        {
        }
    }
}
