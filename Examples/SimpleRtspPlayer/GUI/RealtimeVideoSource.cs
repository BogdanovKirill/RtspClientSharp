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
        private byte[] _decodedFrameBuffer = new byte[0];

        private readonly Dictionary<FFmpegVideoCodecId, FFmpegVideoDecoder> _videoDecodersMap =
            new Dictionary<FFmpegVideoCodecId, FFmpegVideoDecoder>();

        private ulong _desiredSize;

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
            ulong desiredSize = (ulong) width << 32 | (uint) height;
            Volatile.Write(ref _desiredSize, desiredSize);
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

            ulong desiredSize = Volatile.Read(ref _desiredSize);

            int targetWidth;
            int targetHeight;

            int bufferSize;

            if (desiredSize == 0)
            {
                targetWidth = decodedFrameParameters.Width;
                targetHeight = decodedFrameParameters.Height;

                bufferSize = decodedFrameParameters.Height *
                             ImageUtils.GetStride(decodedFrameParameters.Width, PixelFormat.Bgr24);
            }
            else
            {
                targetWidth = (int) (desiredSize >> 32);
                targetHeight = (int) desiredSize;

                bufferSize = targetHeight *
                             ImageUtils.GetStride(targetWidth, PixelFormat.Bgr24);
            }

            if (_decodedFrameBuffer.Length != bufferSize)
                _decodedFrameBuffer = new byte[bufferSize];

            var bufferSegment = new ArraySegment<byte>(_decodedFrameBuffer);

            var postVideoDecodingParameters = new PostVideoDecodingParameters(RectangleF.Empty,
                new Size(targetWidth, targetHeight),
                ScalingPolicy.Stretch, PixelFormat.Bgr24, ScalingQuality.Bicubic);

            IDecodedVideoFrame decodedFrame = decoder.GetDecodedFrame(bufferSegment, postVideoDecodingParameters);

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