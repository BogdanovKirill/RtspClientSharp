using System;

namespace SimpleRtspPlayer.RawFramesDecoding
{
    static class ImageUtils
    {
        public static int GetStride(int width, PixelFormat pixelFormat)
        {
            int bitsPerPixel = pixelFormat.GetBitsPerPixel();

            return ((width * bitsPerPixel + 31) & ~31) >> 3;
        }

        public static int GetBitsPerPixel(this PixelFormat pixelFormat)
        {
            if (pixelFormat == PixelFormat.Grayscale)
                return 8;
            if (pixelFormat == PixelFormat.Bgr24)
                return 24;
            if (pixelFormat == PixelFormat.Abgr32)
                return 32;

            throw new ArgumentOutOfRangeException(nameof(pixelFormat));
        }
    }
}