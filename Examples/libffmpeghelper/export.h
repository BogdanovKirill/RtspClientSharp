#pragma once

#ifdef _WINDLL
#define DllExport(rettype)  extern "C" __declspec(dllexport) rettype __cdecl
#else
#define DllExport(rettype)  extern "C" __attribute__((cdecl)) rettype
#endif

DllExport(int) create_video_decoder(int codec_id, void **handle);
DllExport(int) set_video_decoder_extradata(void *handle, void *extradata, int extradataLength);
DllExport(int) decode_video_frame(void *handle, void *rawBuffer, int rawBufferLength, int *frameWidth, int *frameHeight, int *framePixelFormat);
DllExport(int) scale_decoded_video_frame(void *handle, void *scalerHandle, void *scaledBuffer, int scaledBufferStride);
DllExport(void) remove_video_decoder(void *handle);

DllExport(int) create_video_scaler(int sourceLeft, int sourceTop, int sourceWidth, int sourceHeight, int sourcePixelFormat, 
	int scaledWidth, int scaledHeight, int scaledPixelFormat, int quality, void **handle);
DllExport(void) remove_video_scaler(void *handle);

DllExport(int) create_audio_decoder(int codec_id, int bits_per_coded_sample, void **handle);
DllExport(int) set_audio_decoder_extradata(void *handle, void *extradata, int extradataLength);
DllExport(int) decode_audio_frame(void *handle, void *rawBuffer, int rawBufferLength, int *sampleRate, int *bitsPerSample, int *channels);
DllExport(int) get_decoded_audio_frame(void *handle, void **outBuffer, int *outDataSize);
DllExport(void) remove_audio_decoder(void *handle);

DllExport(int) create_audio_resampler(void *decoderHandle, int outSampleRate, int outBitsPerSample, int outChannels, void **handle);
DllExport(int) resample_decoded_audio_frame(void *decoderHandle, void *resamplerHandle, void **outBuffer, int *outDataSize);
DllExport(void) remove_audio_resampler(void *handle);