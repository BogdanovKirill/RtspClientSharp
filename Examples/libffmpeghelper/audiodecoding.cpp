#include "stdafx.h"

struct AudioDecoderContext
{
	AVCodec *codec;
	AVCodecContext *av_codec_context;
	AVPacket av_raw_packet;
	AVFrame *frame;
	int decoded_size;
};

int create_audio_decoder(int codec_id, int bits_per_coded_sample, void **handle)
{
	if (!handle)
		return -1;

	auto context = static_cast<AudioDecoderContext *>(av_mallocz(sizeof(AudioDecoderContext)));

	if (!context)
		return -2;

	context->codec = avcodec_find_decoder(static_cast<AVCodecID>(codec_id));

	if (!context->codec)
	{
		remove_audio_decoder(context);
		return -3;
	}

	context->av_codec_context = avcodec_alloc_context3(context->codec);
	if (!context->av_codec_context)
	{
		remove_audio_decoder(context);
		return -4;
	}

	if (codec_id == AV_CODEC_ID_PCM_MULAW || codec_id == AV_CODEC_ID_PCM_ALAW)
	{
		context->av_codec_context->sample_rate = 8000;
		context->av_codec_context->channels = 1;
	}

	context->av_codec_context->bits_per_coded_sample = bits_per_coded_sample;
	if (avcodec_open2(context->av_codec_context, context->codec, nullptr) < 0)
	{
		remove_audio_decoder(context);
		return -5;
	}

	context->frame = av_frame_alloc();
	if (!context->frame)
	{
		remove_audio_decoder(context);
		return -6;
	}

	av_init_packet(&context->av_raw_packet);

	*handle = context;
	return 0;
}

int set_audio_decoder_extradata(void *handle, void *extradata, int extradataLength)
{
#if _DEBUG
	if (!handle || !extradata || !extradataLength)
		return -1;
#endif

	auto context = static_cast<AudioDecoderContext *>(handle);

	if (!context->av_codec_context->extradata || context->av_codec_context->extradata_size < extradataLength)
	{
		av_free(context->av_codec_context->extradata);
		context->av_codec_context->extradata = static_cast<uint8_t*>(av_malloc(extradataLength + AV_INPUT_BUFFER_PADDING_SIZE));

		if (!context->av_codec_context->extradata)
			return -2;
	}

	context->av_codec_context->extradata_size = extradataLength;

	memcpy(context->av_codec_context->extradata, extradata, extradataLength);
	memset(context->av_codec_context->extradata + extradataLength, 0, AV_INPUT_BUFFER_PADDING_SIZE);

	avcodec_close(context->av_codec_context);
	if (avcodec_open2(context->av_codec_context, context->codec, nullptr) < 0)
		return -3;

	return 0;
}

int decode_audio_frame(void *handle, void *rawBuffer, int rawBufferLength, int *outSize, int *sampleRate, int *bitsPerSample, int *channels)
{
#if _DEBUG
	if (!handle || !rawBuffer || !rawBufferLength || !outSize || !sampleRate || !bitsPerSample || !channels)
		return -1;

	if (reinterpret_cast<uintptr_t>(rawBuffer) % 4 != 0)
		return -2;
#endif

	auto context = static_cast<AudioDecoderContext *>(handle);

	context->decoded_size = 0;
	context->av_raw_packet.data = static_cast<uint8_t *>(rawBuffer);
	context->av_raw_packet.size = rawBufferLength;

	int got_frame;

	const int len = avcodec_decode_audio4(context->av_codec_context, context->frame, &got_frame, &context->av_raw_packet);

	if (len != rawBufferLength)
		return -3;

	if (got_frame)
	{
		*sampleRate = context->av_codec_context->sample_rate;
		*bitsPerSample = av_get_bytes_per_sample(context->av_codec_context->sample_fmt) * 8;
		*channels = context->av_codec_context->channels;
		context->decoded_size = av_samples_get_buffer_size(nullptr, context->av_codec_context->channels, context->frame->nb_samples, context->av_codec_context->sample_fmt, 1);
		*outSize = context->decoded_size;

		return 0;
	}

	return -4;
}

int get_decoded_audio_frame(void *handle, void *outBuffer)
{
#if _DEBUG
	if (!handle)
		return -1;
#endif

	auto context = static_cast<AudioDecoderContext *>(handle);

	if (context->decoded_size == 0)
		return -2;

	memcpy(outBuffer, context->frame->data[0], context->decoded_size);
	return 0;
}

void remove_audio_decoder(void *handle)
{
	if (!handle)
		return;

	auto context = static_cast<AudioDecoderContext *>(handle);

	if (context->av_codec_context)
	{
		avcodec_close(context->av_codec_context);
		av_free(context->av_codec_context);
	}

	av_frame_free(&context->frame);
	av_free(context);
}