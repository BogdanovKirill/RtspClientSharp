using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RtspClientSharp.RawFrames.Video;
using SimpleRtspPlayer.RawFramesDecoding.DecodedFrames;

namespace SimpleRtspPlayer.RawFramesDecoding.FFmpeg
{
    class FFmpegVideoDecoder
    {
        private readonly IntPtr _decoderHandle;
        private readonly FFmpegVideoCodecId _videoCodecId;
        private DateTime _currentDecodedFrameTimestamp;

        private DecodedVideoFrameParameters _currentFrameParameters =
            new DecodedVideoFrameParameters(0, 0, FFmpegPixelFormat.None);

        private readonly Dictionary<PostVideoDecodingParameters, FFmpegDecodedVideoScaler> _scalersMap =
            new Dictionary<PostVideoDecodingParameters, FFmpegDecodedVideoScaler>();

        private byte[] _extraData = new byte[0];
        private bool _disposed;

        private FFmpegVideoDecoder(FFmpegVideoCodecId videoCodecId, IntPtr decoderHandle)
        {
            _videoCodecId = videoCodecId;
            _decoderHandle = decoderHandle;
        }

        ~FFmpegVideoDecoder()
        {
            Dispose();
        }

        public static FFmpegVideoDecoder CreateDecoder(FFmpegVideoCodecId videoCodecId)
        {
            int resultCode = FFmpegVideoPInvoke.CreateVideoDecoder(videoCodecId, out IntPtr decoderPtr);

            if (resultCode != 0)
                throw new DecoderException(
                    $"An error occurred while creating video decoder for {videoCodecId} codec, code: {resultCode}");

            return new FFmpegVideoDecoder(videoCodecId, decoderPtr);
        }

        public unsafe bool TryDecode(RawVideoFrame rawVideoFrame, out DecodedVideoFrameParameters frameParameters)
        {
            Debug.Assert(rawVideoFrame.FrameSegment.Array != null, "rawVideoFrame.FrameSegment.Array != null");
            fixed (byte* rawBufferPtr = &rawVideoFrame.FrameSegment.Array[rawVideoFrame.FrameSegment.Offset])
            {
                int resultCode;

                if (rawVideoFrame is RawH264IFrame rawH264IFrame)
                {
                    if (rawH264IFrame.SpsPpsSegment.Array != null &&
                        !_extraData.SequenceEqual(rawH264IFrame.SpsPpsSegment))
                    {
                        if (_extraData.Length != rawH264IFrame.SpsPpsSegment.Count)
                            _extraData = new byte[rawH264IFrame.SpsPpsSegment.Count];

                        Buffer.BlockCopy(rawH264IFrame.SpsPpsSegment.Array, rawH264IFrame.SpsPpsSegment.Offset,
                            _extraData, 0, rawH264IFrame.SpsPpsSegment.Count);

                        fixed (byte* initDataPtr = &_extraData[0])
                        {
                            resultCode = FFmpegVideoPInvoke.SetVideoDecoderExtraData(_decoderHandle,
                                (IntPtr) initDataPtr, _extraData.Length);

                            if (resultCode != 0)
                                throw new DecoderException(
                                    $"An error occurred while setting video extra data, {_videoCodecId} codec, code: {resultCode}");
                        }
                    }
                }

                resultCode = FFmpegVideoPInvoke.DecodeFrame(_decoderHandle, (IntPtr) rawBufferPtr,
                    rawVideoFrame.FrameSegment.Count,
                    out int width, out int height, out FFmpegPixelFormat pixelFormat);

                if (resultCode != 0)
                {
                    frameParameters = null;
                    return false;
                }

                _currentDecodedFrameTimestamp = rawVideoFrame.Timestamp;

                if (_currentFrameParameters.Width != width || _currentFrameParameters.Height != height ||
                    _currentFrameParameters.PixelFormat != pixelFormat)
                {
                    _currentFrameParameters = new DecodedVideoFrameParameters(width, height, pixelFormat);
                    DropAllVideoScalers();
                }

                frameParameters = _currentFrameParameters;
                return true;
            }
        }

        public unsafe IDecodedVideoFrame GetDecodedFrame(ArraySegment<byte> bufferSegment,
            PostVideoDecodingParameters parameters)
        {
            if (!_scalersMap.TryGetValue(parameters, out FFmpegDecodedVideoScaler videoScaler))
            {
                videoScaler = FFmpegDecodedVideoScaler.Create(_currentFrameParameters, parameters);
                _scalersMap.Add(parameters, videoScaler);
            }

            int resultCode;

            Debug.Assert(bufferSegment.Array != null, "bufferSegment.Array != null");

            fixed (byte* scaledBuffer = &bufferSegment.Array[bufferSegment.Offset])
                resultCode = FFmpegVideoPInvoke.ScaleDecodedVideoFrame(_decoderHandle, videoScaler.Handle,
                    (IntPtr) scaledBuffer, videoScaler.ScaledStride);

            if (resultCode != 0)
                throw new DecoderException(
                    $"An error occurred while converting decoding video frame, {_videoCodecId} codec, code: {resultCode}");

            return new DecodedVideoFrame(_currentDecodedFrameTimestamp, bufferSegment, _currentFrameParameters.Width,
                _currentFrameParameters.Height,
                videoScaler.ScaledWidth, videoScaler.ScaledHeight, videoScaler.ScaledPixelFormat,
                videoScaler.ScaledStride);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            FFmpegVideoPInvoke.RemoveVideoDecoder(_decoderHandle);
            DropAllVideoScalers();
            GC.SuppressFinalize(this);
        }

        private void DropAllVideoScalers()
        {
            foreach (var scaler in _scalersMap.Values)
                scaler.Dispose();

            _scalersMap.Clear();
        }
    }
}