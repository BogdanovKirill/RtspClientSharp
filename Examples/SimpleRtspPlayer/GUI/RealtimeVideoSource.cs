using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using RtspClientSharp.RawFrames;
using RtspClientSharp.RawFrames.Video;
using SimpleRtspPlayer.RawFramesDecoding;
using SimpleRtspPlayer.RawFramesDecoding.DecodedFrames;
using SimpleRtspPlayer.RawFramesDecoding.FFmpeg;
using SimpleRtspPlayer.RawFramesReceiving;

namespace SimpleRtspPlayer.GUI
{
    class RealtimeVideoSource : IVideoSource, IDisposable
    {
        private IRawFramesSource _rawFramesSource;

        private readonly Dictionary<FFmpegVideoCodecId, FFmpegVideoDecoder> _videoDecodersMap =
            new Dictionary<FFmpegVideoCodecId, FFmpegVideoDecoder>();

        private long _desiredSize;

        public event EventHandler<IDecodedVideoFrame> FrameReceived;

        public void SetRawFramesSource(IRawFramesSource rawFramesSource)
        {
            if (_rawFramesSource != null)
            {
                _rawFramesSource.FrameReceived -= OnFrameReceived;
                DropAllVideoDecoders();
            }

            _rawFramesSource = rawFramesSource;

            if (rawFramesSource == null)
                return;

            rawFramesSource.FrameReceived += OnFrameReceived;
        }

        public void SetVideoSize(int width, int height)
        {
            long desiredSize = (long) width << 32 | (uint) height;
            Interlocked.Exchange(ref _desiredSize, desiredSize);
        }

        public void Dispose()
        {
            DropAllVideoDecoders();
        }

        private void DropAllVideoDecoders()
        {
            foreach (FFmpegVideoDecoder decoder in _videoDecodersMap.Values)
                decoder.Dispose();

            _videoDecodersMap.Clear();
        }

        private void OnFrameReceived(object sender, RawFrame rawFrame)
        {
            if (!(rawFrame is RawVideoFrame rawVideoFrame))
                return;

            FFmpegVideoDecoder decoder = GetDecoderForFrame(rawVideoFrame);

            if (!decoder.TryDecode(rawVideoFrame, out DecodedVideoFrameParameters decodedFrameParameters))
                return;

            long desiredSize = Interlocked.Read(ref _desiredSize);

            int targetWidth;
            int targetHeight;

            if (desiredSize == 0)
            {
                targetWidth = decodedFrameParameters.Width;
                targetHeight = decodedFrameParameters.Height;
            }
            else
            {
                targetWidth = (int) (desiredSize >> 32);
                targetHeight = (int) desiredSize;
            }

            var postVideoDecodingParameters = new PostVideoDecodingParameters(RectangleF.Empty,
                new Size(targetWidth, targetHeight),
                ScalingPolicy.Stretch, PixelFormat.Bgr24, ScalingQuality.Bicubic);

            IDecodedVideoFrame decodedFrame = decoder.GetDecodedFrame(postVideoDecodingParameters);

            FrameReceived?.Invoke(this, decodedFrame);
        }

        private FFmpegVideoDecoder GetDecoderForFrame(RawVideoFrame videoFrame)
        {
            FFmpegVideoCodecId codecId = DetectCodecId(videoFrame);
            if (!_videoDecodersMap.TryGetValue(codecId, out FFmpegVideoDecoder decoder))
            {
                decoder = FFmpegVideoDecoder.CreateDecoder(codecId);
                _videoDecodersMap.Add(codecId, decoder);
            }

            return decoder;
        }

        private FFmpegVideoCodecId DetectCodecId(RawVideoFrame videoFrame)
        {
            if (videoFrame is RawJpegFrame)
                return FFmpegVideoCodecId.MJPEG;
            if (videoFrame is RawH264Frame)
                return FFmpegVideoCodecId.H264;

            throw new ArgumentOutOfRangeException(nameof(videoFrame));
        }
    }
}