using System;
using System.Runtime.InteropServices;

namespace SimpleRtspPlayer.RawFramesDecoding.FFmpeg
{
    class FFmpegAudioPInvoke
    {
        private const string LibraryName = "libffmpeghelper.dll";

        [DllImport(LibraryName, EntryPoint = "create_audio_decoder", CallingConvention = CallingConvention.Cdecl)]
        public static extern int CreateAudioDecoder(FFmpegAudioCodecId audioCodecId, int bitsPerCodedSample, out IntPtr handle);

        [DllImport(LibraryName, EntryPoint = "set_audio_decoder_extradata", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SetAudioDecoderExtraData(IntPtr handle, IntPtr extradata, int extradataLength);

        [DllImport(LibraryName, EntryPoint = "remove_audio_decoder", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RemoveAudioDecoder(IntPtr handle);

        [DllImport(LibraryName, EntryPoint = "decode_audio_frame", CallingConvention = CallingConvention.Cdecl)]
        public static extern int DecodeFrame(IntPtr handle, IntPtr rawBuffer, int rawBufferLength, out int outSize, out int sampleRate, out int bitsPerSample, out int channels);

        [DllImport(LibraryName, EntryPoint = "get_decoded_audio_frame", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetDecodedFrame(IntPtr handle, IntPtr outBuffer);
    }
}
