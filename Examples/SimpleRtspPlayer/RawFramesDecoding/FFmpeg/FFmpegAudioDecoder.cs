using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using RtspClientSharp.RawFrames.Audio;
using SimpleRtspPlayer.RawFramesDecoding.DecodedFrames;

namespace SimpleRtspPlayer.RawFramesDecoding.FFmpeg
{
    class FFmpegAudioDecoder
    {
        private readonly IntPtr _decoderHandle;
        private readonly FFmpegAudioCodecId _audioCodecId;
        private IntPtr _resamplerHandle;
        private AudioFrameFormat _currentFrameFormat = new AudioFrameFormat(0, 0, 0);
        private DateTime _currentRawFrameTimestamp;
        private byte[] _extraData = new byte[0];
        private byte[] _decodedFrameBuffer = new byte[0];
        private bool _disposed;

        // Lock object to ensure this object remains disposed (or not) for the duration of a action
        private readonly object disposalLock = new object();

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
        public unsafe bool TryDecode(RawAudioFrame rawAudioFrame)
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
                lock (disposalLock) {
                    if (_disposed) {
                        Console.WriteLine("Skipped decoding audio frame, as decoder was disposed. (Therefore the frame probably wasn't wanted)");
                        return false;
                    }

                    int resultCode = FFmpegAudioPInvoke.DecodeFrame(_decoderHandle, (IntPtr)rawBufferPtr,
                    rawAudioFrame.FrameSegment.Count, out int sampleRate, out int bitsPerSample, out int channels);

                    _currentRawFrameTimestamp = rawAudioFrame.Timestamp;

                    if (resultCode != 0)
                        return false;

                    if (rawAudioFrame is RawG711Frame g711Frame) {
                        sampleRate = g711Frame.SampleRate;
                        channels = g711Frame.Channels;
                    }


                    if (_currentFrameFormat.SampleRate != sampleRate || _currentFrameFormat.BitPerSample != bitsPerSample ||
                        _currentFrameFormat.Channels != channels) {
                        _currentFrameFormat = new AudioFrameFormat(sampleRate, bitsPerSample, channels);

                        if (_resamplerHandle != IntPtr.Zero)
                            FFmpegAudioPInvoke.RemoveAudioResampler(_resamplerHandle);
                    }
                }
            }

            return true;
        }

        /// <exception cref="DecoderException"></exception>
        public IDecodedAudioFrame GetDecodedFrame(AudioConversionParameters optionalAudioConversionParameters = null)
        {
            IntPtr outBufferPtr;
            int dataSize;

            AudioFrameFormat format;

            int resultCode;

            lock (disposalLock) {
                if (_disposed) {
                    Console.WriteLine("Skipped retrieving decoded audio frame, as decoder was disposed. (Therefore the frame probably wasn't wanted)");
                    return null;
                }

                if (optionalAudioConversionParameters == null ||
                (optionalAudioConversionParameters.OutSampleRate == 0 || optionalAudioConversionParameters.OutSampleRate == _currentFrameFormat.SampleRate) &&
                (optionalAudioConversionParameters.OutBitsPerSample == 0 || optionalAudioConversionParameters.OutBitsPerSample == _currentFrameFormat.BitPerSample) &&
                (optionalAudioConversionParameters.OutChannels == 0 || optionalAudioConversionParameters.OutChannels == _currentFrameFormat.Channels)) {
                    resultCode = FFmpegAudioPInvoke.GetDecodedFrame(_decoderHandle, out outBufferPtr, out dataSize);

                    if (resultCode != 0)
                        throw new DecoderException($"An error occurred while getting decoded audio frame, {_audioCodecId} codec, code: {resultCode}");

                    format = _currentFrameFormat;
                } else {
                    if (_resamplerHandle == IntPtr.Zero) {
                        resultCode = FFmpegAudioPInvoke.CreateAudioResampler(_decoderHandle,
                            optionalAudioConversionParameters.OutSampleRate, optionalAudioConversionParameters.OutBitsPerSample,
                            optionalAudioConversionParameters.OutChannels, out _resamplerHandle);

                        if (resultCode != 0)
                            throw new DecoderException($"An error occurred while creating audio resampler, code: {resultCode}");
                    }

                    resultCode = FFmpegAudioPInvoke.ResampleDecodedFrame(_decoderHandle, _resamplerHandle, out outBufferPtr, out dataSize);

                    if (resultCode != 0)
                        throw new DecoderException($"An error occurred while converting audio frame, code: {resultCode}");

                    format = new AudioFrameFormat(optionalAudioConversionParameters.OutSampleRate != 0 ? optionalAudioConversionParameters.OutSampleRate : _currentFrameFormat.SampleRate,
                        optionalAudioConversionParameters.OutBitsPerSample != 0 ? optionalAudioConversionParameters.OutBitsPerSample : _currentFrameFormat.BitPerSample,
                        optionalAudioConversionParameters.OutChannels != 0 ? optionalAudioConversionParameters.OutChannels : _currentFrameFormat.Channels);
                }
            }

            if (_decodedFrameBuffer.Length < dataSize)
                _decodedFrameBuffer = new byte[dataSize];

            Marshal.Copy(outBufferPtr, _decodedFrameBuffer, 0, dataSize);
            return new DecodedAudioFrame(_currentRawFrameTimestamp, new ArraySegment<byte>(_decodedFrameBuffer, 0, dataSize), format);
        }

        public void Dispose()
        {
            lock (disposalLock) {
                if (_disposed)
                    return;

                _disposed = true;
                FFmpegAudioPInvoke.RemoveAudioDecoder(_decoderHandle);

                if (_resamplerHandle != IntPtr.Zero)
                    FFmpegAudioPInvoke.RemoveAudioResampler(_resamplerHandle);

                GC.SuppressFinalize(this);
            }
        }
    }
}
