using System;

namespace RtspClientSharp.RawFrames.Video
{
    public class RawJpegFrame : RawVideoFrame
    {
        public static readonly byte[] StartMarkerBytes = {0xFF, 0xD8};
        public static readonly byte[] EndMarkerBytes = {0xFF, 0xD9};

        public RawJpegFrame(DateTime timestamp, ArraySegment<byte> frameSegment)
            : base(timestamp, frameSegment)
        {
        }
    }
}