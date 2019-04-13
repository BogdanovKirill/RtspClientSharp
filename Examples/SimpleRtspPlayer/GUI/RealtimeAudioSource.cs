using System;
using System.Collections.Generic;
using RtspClientSharp.RawFrames;
using RtspClientSharp.RawFrames.Audio;
using SimpleRtspPlayer.RawFramesDecoding.DecodedFrames;
using SimpleRtspPlayer.RawFramesDecoding.FFmpeg;
using SimpleRtspPlayer.RawFramesReceiving;

namespace SimpleRtspPlayer.GUI
{
    class RealtimeAudioSource : IAudioSource
    {
        private IRawFramesSource _rawFramesSource;
        private byte[] _decodedFrameBuffer = new byte[0];

        private readonly Dictionary<FFmpegAudioCodecId, FFmpegAudioDecoder> _audioDecodersMap =
            new Dictionary<FFmpegAudioCodecId, FFmpegAudioDecoder>();

        public event EventHandler<IDecodedAudioFrame> FrameReceived;

        public void SetRawFramesSource(IRawFramesSource rawFramesSource)
        {
            if (_rawFramesSource != null)
                _rawFramesSource.FrameReceived -= OnFrameReceived;

            _rawFramesSource = rawFramesSource;

            if (rawFramesSource == null)
                return;

            rawFramesSource.FrameReceived += OnFrameReceived;
        }

        private void OnFrameReceived(object sender, RawFrame rawFrame)
        {
            if (!(rawFrame is RawAudioFrame rawAudioFrame))
                return;

            FFmpegAudioDecoder decoder = GetDecoderForFrame(rawAudioFrame);

            if (!decoder.TryDecode(rawAudioFrame, out int decodedFrameSize))
                return;

            if (_decodedFrameBuffer.Length < decodedFrameSize)
                _decodedFrameBuffer = new byte[decodedFrameSize];

            var bufferSegment = new ArraySegment<byte>(_decodedFrameBuffer, 0, decodedFrameSize);

            IDecodedAudioFrame decodedFrame = decoder.GetDecodedFrame(bufferSegment);

            FrameReceived?.Invoke(this, decodedFrame);
        }

        private FFmpegAudioDecoder GetDecoderForFrame(RawAudioFrame audioFrame)
        {
            FFmpegAudioCodecId codecId = DetectCodecId(audioFrame);

            if (!_audioDecodersMap.TryGetValue(codecId, out FFmpegAudioDecoder decoder))
            {
                int bitsPerCodedSample = 0;

                if (audioFrame is RawG726Frame g726Frame)
                    bitsPerCodedSample = g726Frame.BitsPerCodedSample;

                decoder = FFmpegAudioDecoder.CreateDecoder(codecId, bitsPerCodedSample);
                _audioDecodersMap.Add(codecId, decoder);
            }

            return decoder;
        }

        private FFmpegAudioCodecId DetectCodecId(RawAudioFrame audioFrame)
        {
            if (audioFrame is RawAACFrame)
                return FFmpegAudioCodecId.AAC;
            if (audioFrame is RawG711AFrame)
                return FFmpegAudioCodecId.G711A;
            if (audioFrame is RawG711UFrame)
                return FFmpegAudioCodecId.G711U;
            if (audioFrame is RawG726Frame)
                return FFmpegAudioCodecId.G726;

            throw new ArgumentOutOfRangeException(nameof(audioFrame));
        }
    }
}
