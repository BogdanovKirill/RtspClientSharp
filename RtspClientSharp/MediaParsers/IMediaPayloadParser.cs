using System;
using RtspClientSharp.RawFrames;

namespace RtspClientSharp.MediaParsers
{
    interface IMediaPayloadParser
    {
        DateTime BaseTime { get; set; }

        Action<RawFrame> FrameGenerated { get; set; }
        
        Action<byte[]> NaluReceived { get; set; }

        void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit);

        void ResetState();
    }
}