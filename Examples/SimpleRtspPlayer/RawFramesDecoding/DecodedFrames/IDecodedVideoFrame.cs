using System;

namespace SimpleRtspPlayer.RawFramesDecoding.DecodedFrames
{
    public interface IDecodedVideoFrame
    {
        DateTime Timestamp { get; }
        ArraySegment<byte> DecodedBytes { get; }
        int OriginalWidth { get; }
        int OriginalHeight { get; }
        int Width { get; }
        int Height { get; }
        PixelFormat Format { get; }
        int Stride { get; }
    }
}