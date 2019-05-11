#include "stdafx.h"

struct AudioDecoderContext
{
	AVCodec *codec;
	AVCodecContext *av_codec_context;
	AVPacket av_raw_packet;
	AVFrame *frame;
};

struct AudioResamplerContext
{
	SwrContext *swr_context;
	uint8_t **out_data;
	int out_linesize;
    int64_t out_nb_samples;
	int out_sample_rate;
	int out_channels;
	AVSampleFormat out_sample_format;
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

int decode_audio_frame(void *handle, void *rawBuffer, int rawBufferLength, int *sampleRate, int *bitsPerSample, int *channels)
{
#if _DEBUG
	if (!handle || !rawBuffer || !rawBufferLength || !sampleRate || !bitsPerSample || !channels)
		return -1;

	if (reinterpret_cast<uintptr_t>(rawBuffer) % 4 != 0)
		return -2;
#endif

	auto context = static_cast<AudioDecoderContext *>(handle);

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

		return 0;
	}

	return -4;
}

int get_decoded_audio_frame(void *handle, void **outBuffer, int *outDataSize)
{
#if _DEBUG
	if (!handle)
		return -1;
#endif

	auto context = static_cast<AudioDecoderContext *>(handle);

	*reinterpret_cast<uint8_t **>(outBuffer) = context->frame->data[0];
	*outDataSize = av_samples_get_buffer_size(nullptr, context->av_codec_context->channels, context->frame->nb_samples, context->av_codec_context->sample_fmt, 1);;
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

int create_audio_resampler(void *decoderHandle, int outSampleRate, int outBitsPerSample, int outChannels, void **handle)
{
#if _DEBUG
	if (!handle)
		return -1;
#endif
	const auto decoder_context = static_cast<AudioDecoderContext *>(decoderHandle);

#if _DEBUG
	if (!decoder_context)
		return -2;
#endif

	const int out_sample_rate = outSampleRate != 0 ? outSampleRate : decoder_context->av_codec_context->sample_rate;
	
	AVSampleFormat out_sample_format;

	if (outBitsPerSample != 0)
	{
		if (outBitsPerSample == 8)
			out_sample_format = AV_SAMPLE_FMT_U8;
		else if (outBitsPerSample == 16)
			out_sample_format = AV_SAMPLE_FMT_S16;
		else
			return -3;
	}
	else
		out_sample_format = decoder_context->av_codec_context->sample_fmt;

	int out_channels;
	int64_t out_channel_layout;

	if (outChannels != 0)
	{
		out_channels = outChannels;
		out_channel_layout = av_get_default_channel_layout(outChannels);
	}
	else
	{
		out_channel_layout = decoder_context->av_codec_context->channel_layout;
		out_channels = decoder_context->av_codec_context->channels;
	}

	const auto resampler_context = static_cast<AudioResamplerContext *>(av_mallocz(sizeof(AudioResamplerContext)));

	if (!resampler_context)
		return -4;

	const int64_t in_channel_layout = decoder_context->av_codec_context->channel_layout;

	resampler_context->swr_context = swr_alloc_set_opts(nullptr, out_channel_layout, out_sample_format, out_sample_rate, in_channel_layout, 
		decoder_context->av_codec_context->sample_fmt, decoder_context->av_codec_context->sample_rate, 0, nullptr);
	
	if (resampler_context->swr_context == nullptr)
	{
		av_free(resampler_context);
		return -5;
	}

	if(swr_init(resampler_context->swr_context) < 0)
	{
		remove_audio_resampler(resampler_context);
		return -6;
	}
	
	resampler_context->out_sample_rate = out_sample_rate;
	resampler_context->out_channels = out_channels;
	resampler_context->out_sample_format = out_sample_format;

	*handle = resampler_context;
	return 0;
}

int resample_decoded_audio_frame(void *decoderHandle, void *resamplerHandle, void **outBuffer, int *outDataSize)
{
#if _DEBUG
	if (!decoderHandle || !resamplerHandle || !outBuffer || !outDataSize)
		return -1;
#endif

	const auto decoder_context = static_cast<AudioDecoderContext *>(decoderHandle);
	const auto resampler_context = static_cast<AudioResamplerContext *>(resamplerHandle);

	const int out_nb_samples = static_cast<int>(av_rescale_rnd(swr_get_delay(resampler_context->swr_context, decoder_context->frame->sample_rate) +
	                                                           decoder_context->frame->nb_samples, resampler_context->out_sample_rate,
	                                                           decoder_context->frame->sample_rate, AV_ROUND_UP));
	if (out_nb_samples > resampler_context->out_nb_samples)
	{
		if (resampler_context->out_data)
			av_freep(&resampler_context->out_data[0]);

		if (av_samples_alloc_array_and_samples(&resampler_context->out_data, &resampler_context->out_linesize, resampler_context->out_channels,
			out_nb_samples, resampler_context->out_sample_format, 0) < 0)
			return -2;

		resampler_context->out_nb_samples = out_nb_samples;
	}

	const int ret = swr_convert(resampler_context->swr_context, resampler_context->out_data, out_nb_samples, const_cast<const uint8_t **>(decoder_context->frame->data), decoder_context->frame->nb_samples);

	if(ret < 0)
		return -3;

	*reinterpret_cast<uint8_t **>(outBuffer) = resampler_context->out_data[0];
	
	*outDataSize = av_samples_get_buffer_size(&resampler_context->out_linesize, resampler_context->out_channels,
		ret, resampler_context->out_sample_format, 1);;

	return 0;
}


void remove_audio_resampler(void *handle)
{
	if (!handle)
		return;

	auto resampler_context = static_cast<AudioResamplerContext *>(handle);

	if (resampler_context->out_data)
	{
		av_freep(&resampler_context->out_data[0]);
		av_freep(&resampler_context->out_data);
	}

	swr_free(&resampler_context->swr_context);
	av_free(resampler_context);
}