using System;

namespace SimpleRtspPlayer.RawFramesDecoding.DecodedFrames
{
    class DecodedVideoFrame : IDecodedVideoFrame
    {
        public DateTime Timestamp { get; }
        public ArraySegment<byte> DecodedBytes { get; }
        public int OriginalWidth { get; }
        public int OriginalHeight { get; }
        public int Width { get; }
        public int Height { get; }
        public PixelFormat Format { get; }
        public int Stride { get; }

        public DecodedVideoFrame(DateTime timestamp, ArraySegment<byte> decodedBytes, int originalWidth,
            int originalHeight, int width, int height, PixelFormat format, int stride)
        {
            Timestamp = timestamp;
            DecodedBytes = decodedBytes;
            OriginalWidth = originalWidth;
            OriginalHeight = originalHeight;
            Width = width;
            Height = height;
            Format = format;
            Stride = stride;
        }
    }
}