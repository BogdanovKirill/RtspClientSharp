using System;
using System.Collections.Generic;
using RtspClientSharp.RawFrames;
using RtspClientSharp.RawFrames.Video;
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

            IDecodedVideoFrame decodedFrame = decoder.TryDecode(rawVideoFrame);

            if (decodedFrame != null)
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
            if (videoFrame is RawH265Frame)
                return FFmpegVideoCodecId.H265;

            throw new ArgumentOutOfRangeException(nameof(videoFrame));
        }
    }
}