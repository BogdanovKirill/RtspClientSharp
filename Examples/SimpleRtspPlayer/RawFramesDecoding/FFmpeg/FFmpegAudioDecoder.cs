using System;
using System.Diagnostics;
using System.Linq;
using RtspClientSharp.RawFrames.Audio;
using SimpleRtspPlayer.RawFramesDecoding.DecodedFrames;

namespace SimpleRtspPlayer.RawFramesDecoding.FFmpeg
{
    class FFmpegAudioDecoder
    {
        private readonly IntPtr _decoderHandle;
        private readonly FFmpegAudioCodecId _audioCodecId;
        private AudioFrameFormat _currentFrameFormat = new AudioFrameFormat(0, 0, 0);
        private DateTime _currentRawFrameTimestamp;
        private byte[] _extraData = new byte[0];
        private bool _disposed;

        public int BitsPerCodedSample { get; }

        private FFmpegAudioDecoder(FFmpegAudioCodecId audioCodecId, int bitsPerCodedSample, IntPtr decoderHandle)
        {
            _audioCodecId = audioCodecId;
            BitsPerCodedSample = bitsPerCodedSample;
            _decoderHandle = decoderHandle;
        }

        ~FFmpegAudioDecoder()
        {
            Dispose();
        }

        /// <exception cref="DecoderException"></exception>
        public static FFmpegAudioDecoder CreateDecoder(FFmpegAudioCodecId audioCodecId, int bitsPerCodedSample)
        {
            int resultCode = FFmpegAudioPInvoke.CreateAudioDecoder(audioCodecId, bitsPerCodedSample, out IntPtr decoderPtr);

            if (resultCode != 0)
                throw new DecoderException($"An error occurred while creating audio decoder for {audioCodecId} codec, code: {resultCode}");

            return new FFmpegAudioDecoder(audioCodecId, bitsPerCodedSample, decoderPtr);
        }

        /// <exception cref="DecoderException"></exception>
        public unsafe bool TryDecode(RawAudioFrame rawAudioFrame, out int decodedFrameSize)
        {
            if (rawAudioFrame is RawAACFrame aacFrame)
            {
                Debug.Assert(aacFrame.ConfigSegment.Array != null, "aacFrame.ConfigSegment.Array != null");

                if (!_extraData.SequenceEqual(aacFrame.ConfigSegment))
                {
                    if (_extraData.Length == aacFrame.ConfigSegment.Count)
                        Buffer.BlockCopy(aacFrame.ConfigSegment.Array, aacFrame.ConfigSegment.Offset, _extraData, 0,
                            aacFrame.ConfigSegment.Count);
                    else
                        _extraData = aacFrame.ConfigSegment.ToArray();
                    
                    fixed (byte* extradataPtr = &_extraData[0])
                    {
                        int resultCode = FFmpegAudioPInvoke.SetAudioDecoderExtraData(_decoderHandle, (IntPtr)extradataPtr, aacFrame.ConfigSegment.Count);

                        if (resultCode != 0)
                            throw new DecoderException($"An error occurred while setting audio extra data, {_audioCodecId} codec, code: {resultCode}");
                    }
                }
            }

            Debug.Assert(rawAudioFrame.FrameSegment.Array != null, "rawAudioFrame.FrameSegment.Array != null");

            fixed (byte* rawBufferPtr = &rawAudioFrame.FrameSegment.Array[rawAudioFrame.FrameSegment.Offset])
            {
                int sampleRate;
                int bitsPerSample;
                int channels;

                int resultCode = FFmpegAudioPInvoke.DecodeFrame(_decoderHandle, (IntPtr)rawBufferPtr, rawAudioFrame.FrameSegment.Count,
                    out decodedFrameSize, out sampleRate, out bitsPerSample, out channels);

                _currentRawFrameTimestamp = rawAudioFrame.Timestamp;

                if (resultCode != 0)
                    return false;
                
                if (rawAudioFrame is RawG711Frame g711Frame)
                {
                    sampleRate = g711Frame.SampleRate;
                    channels = g711Frame.Channels;
                }

                if (_currentFrameFormat.SampleRate != sampleRate || _currentFrameFormat.BitPerSample != bitsPerSample || _currentFrameFormat.Channels != channels)
                    _currentFrameFormat = new AudioFrameFormat(sampleRate, bitsPerSample, channels);
            }

            return true;
        }

        /// <exception cref="DecoderException"></exception>
        public unsafe IDecodedAudioFrame GetDecodedFrame(ArraySegment<byte> bufferSegment)
        {
            Debug.Assert(bufferSegment.Array != null, "bufferSegment.Array != null");

            fixed (byte* outByteSegmentPtr = &bufferSegment.Array[bufferSegment.Offset])
            {
                int resultCode = FFmpegAudioPInvoke.GetDecodedFrame(_decoderHandle, (IntPtr)outByteSegmentPtr);

                if (resultCode != 0)
                    throw new DecoderException($"An error occurred while getting decoded audio frame, {_audioCodecId} codec, code: {resultCode}");
            }

            return new DecodedAudioFrame(_currentRawFrameTimestamp, bufferSegment, _currentFrameFormat);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            FFmpegAudioPInvoke.RemoveAudioDecoder(_decoderHandle);
            GC.SuppressFinalize(this);
        }
    }
}
