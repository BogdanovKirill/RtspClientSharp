using System;

namespace SimpleRtspPlayer.RawFramesDecoding.DecodedFrames
{
    public interface IDecodedAudioFrame
    {
        DateTime Timestamp { get; }
        ArraySegment<byte> DecodedBytes { get; }
        AudioFrameFormat Format { get; }
    }
}