using System;

namespace SimpleRtspPlayer.RawFramesDecoding.FFmpeg
{
    class FFmpegDecodedVideoScaler
    {
        private const double MaxAspectRatioError = 0.1;
        private bool _disposed;

        public IntPtr Handle { get; }
        public int ScaledWidth { get; }
        public int ScaledHeight { get; }
        public int ScaledStride { get; }
        public PixelFormat ScaledPixelFormat { get; }

        private FFmpegDecodedVideoScaler(IntPtr handle, int scaledWidth, int scaledHeight,
            PixelFormat scaledPixelFormat)
        {
            Handle = handle;
            ScaledWidth = scaledWidth;
            ScaledHeight = scaledHeight;
            ScaledPixelFormat = scaledPixelFormat;
            ScaledStride = ImageUtils.GetStride(scaledWidth, scaledPixelFormat);
        }

        ~FFmpegDecodedVideoScaler()
        {
            Dispose();
        }

        /// <exception cref="DecoderException"></exception>
        public static FFmpegDecodedVideoScaler Create(DecodedVideoFrameParameters decodedVideoFrameParameters,
            PostVideoDecodingParameters postVideoDecodingParameters)
        {
            if (decodedVideoFrameParameters == null)
                throw new ArgumentNullException(nameof(decodedVideoFrameParameters));
            if (postVideoDecodingParameters == null)
                throw new ArgumentNullException(nameof(postVideoDecodingParameters));

            int sourceLeft = 0;
            int sourceTop = 0;
            int sourceWidth = decodedVideoFrameParameters.Width;
            int sourceHeight = decodedVideoFrameParameters.Height;
            int scaledWidth = decodedVideoFrameParameters.Width;
            int scaledHeight = decodedVideoFrameParameters.Height;

            if (!postVideoDecodingParameters.RegionOfInterest.IsEmpty)
            {
                sourceLeft =
                    (int) (decodedVideoFrameParameters.Width * postVideoDecodingParameters.RegionOfInterest.Left);
                sourceTop =
                    (int) (decodedVideoFrameParameters.Height * postVideoDecodingParameters.RegionOfInterest.Top);
                sourceWidth =
                    (int) (decodedVideoFrameParameters.Width * postVideoDecodingParameters.RegionOfInterest.Width);
                sourceHeight =
                    (int) (decodedVideoFrameParameters.Height * postVideoDecodingParameters.RegionOfInterest.Height);
            }

            if (!postVideoDecodingParameters.TargetFrameSize.IsEmpty)
            {
                scaledWidth = postVideoDecodingParameters.TargetFrameSize.Width;
                scaledHeight = postVideoDecodingParameters.TargetFrameSize.Height;

                ScalingPolicy scalingPolicy = postVideoDecodingParameters.ScalePolicy;

                float srcAspectRatio = (float) sourceWidth / sourceHeight;
                float destAspectRatio = (float) scaledWidth / scaledHeight;

                if (scalingPolicy == ScalingPolicy.Auto)
                {
                    float relativeChange = Math.Abs(srcAspectRatio - destAspectRatio) / srcAspectRatio;

                    scalingPolicy = relativeChange > MaxAspectRatioError
                        ? ScalingPolicy.RespectAspectRatio
                        : ScalingPolicy.Stretch;
                }

                if (scalingPolicy == ScalingPolicy.RespectAspectRatio)
                {
                    if (destAspectRatio < srcAspectRatio)
                        scaledHeight = sourceHeight * scaledWidth / sourceWidth;
                    else
                        scaledWidth = sourceWidth * scaledHeight / sourceHeight;
                }
            }

            PixelFormat scaledPixelFormat = postVideoDecodingParameters.TargetFormat;
            FFmpegPixelFormat scaledFFmpegPixelFormat = GetFFmpegPixelFormat(scaledPixelFormat);
            FFmpegScalingQuality scaleQuality = GetFFmpegScaleQuality(postVideoDecodingParameters.ScaleQuality);

            int resultCode = FFmpegVideoPInvoke.CreateVideoScaler(sourceLeft, sourceTop, sourceWidth, sourceHeight,
                decodedVideoFrameParameters.PixelFormat,
                scaledWidth, scaledHeight, scaledFFmpegPixelFormat, scaleQuality, out var handle);

            if (resultCode != 0)
                throw new DecoderException(@"An error occurred while creating scaler, code: {resultCode}");

            return new FFmpegDecodedVideoScaler(handle, scaledWidth, scaledHeight, scaledPixelFormat);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            FFmpegVideoPInvoke.RemoveVideoScaler(Handle);
            GC.SuppressFinalize(this);
        }

        private static FFmpegScalingQuality GetFFmpegScaleQuality(ScalingQuality scalingQuality)
        {
            if (scalingQuality == ScalingQuality.Nearest)
                return FFmpegScalingQuality.Point;
            if (scalingQuality == ScalingQuality.Bilinear)
                return FFmpegScalingQuality.Bilinear;
            if (scalingQuality == ScalingQuality.Bicubic)
                return FFmpegScalingQuality.Bicubic;

            throw new ArgumentOutOfRangeException(nameof(scalingQuality));
        }

        private static FFmpegPixelFormat GetFFmpegPixelFormat(PixelFormat pixelFormat)
        {
            if (pixelFormat == PixelFormat.Abgr32)
                return FFmpegPixelFormat.BGRA;
            if (pixelFormat == PixelFormat.Grayscale)
                return FFmpegPixelFormat.GRAY8;
            if (pixelFormat == PixelFormat.Bgr24)
                return FFmpegPixelFormat.BGR24;

            throw new ArgumentOutOfRangeException(nameof(pixelFormat));
        }
    }
}