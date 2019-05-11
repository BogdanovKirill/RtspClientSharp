#include "stdafx.h"

struct VideoDecoderContext
{
	AVCodec *codec;
	AVCodecContext *av_codec_context;
	AVPacket av_raw_packet;
	AVFrame *frame;
};

struct ScalerContext
{
	SwsContext *sws_context;
	int source_left;
	int source_top;
	int source_height;
	AVPixelFormat source_pixel_format;
	int scaled_width;
	int scaled_height;
	AVPixelFormat scaled_pixel_format;
};

int create_video_decoder(int codec_id, void **handle)
{
	if (!handle)
		return -1;

	auto context = static_cast<VideoDecoderContext *>(av_mallocz(sizeof(VideoDecoderContext)));

	if (!context)
		return -2;

	context->codec = avcodec_find_decoder(static_cast<AVCodecID>(codec_id));
	if (!context->codec)
	{
		remove_video_decoder(context);
		return -3;
	}

	context->av_codec_context = avcodec_alloc_context3(context->codec);
	if (!context->av_codec_context)
	{
		remove_video_decoder(context);
		return -4;
	}

	if (avcodec_open2(context->av_codec_context, context->codec, nullptr) < 0)
	{
		remove_video_decoder(context);
		return -5;
	}

	context->frame = av_frame_alloc();
	if (!context->frame)
	{
		remove_video_decoder(context);
		return -6;
	}

	av_init_packet(&context->av_raw_packet);

	*handle = context;
	return 0;
}

int set_video_decoder_extradata(void *handle, void *extradata, int extradataLength)
{
#if _DEBUG
	if (!handle || !extradata || !extradataLength)
		return -1;
#endif

	const auto context = static_cast<VideoDecoderContext *>(handle);

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

int decode_video_frame(void *handle, void *rawBuffer, int rawBufferLength, int *frameWidth, int *frameHeight, int *framePixelFormat)
{
#if _DEBUG
	if (!handle || !rawBuffer || !rawBufferLength || !frameWidth || !frameHeight || !framePixelFormat)
		return -1;

	if (reinterpret_cast<uintptr_t>(rawBuffer) % 4 != 0)
		return -2;
#endif

	auto context = static_cast<VideoDecoderContext *>(handle);

	context->av_raw_packet.data = static_cast<uint8_t *>(rawBuffer);
	context->av_raw_packet.size = rawBufferLength;

	int got_frame;

	const int len = avcodec_decode_video2(context->av_codec_context, context->frame, &got_frame, &context->av_raw_packet);

	if (len != rawBufferLength)
		return -3;

	if (got_frame)
	{
		*frameWidth = context->av_codec_context->width;
		*frameHeight = context->av_codec_context->height;
		*framePixelFormat = context->av_codec_context->pix_fmt;
		return 0;
	}

	return -4;
}

int scale_decoded_video_frame(void *handle, void *scalerHandle, void *scaledBuffer, int scaledBufferStride)
{
#if _DEBUG
	if (!handle || !scalerHandle || !scaledBuffer)
		return -1;
#endif

	auto context = static_cast<VideoDecoderContext *>(handle);
	const auto scalerContext = static_cast<ScalerContext *>(scalerHandle);

	if (scalerContext->source_top != 0 || scalerContext->source_left != 0)
	{
		const AVPixFmtDescriptor *sourceFmtDesc = av_pix_fmt_desc_get(scalerContext->source_pixel_format);

		if (!sourceFmtDesc)
			return -4;

		const int x_shift = sourceFmtDesc->log2_chroma_w;
		const int y_shift = sourceFmtDesc->log2_chroma_h;
		
		uint8_t *srcData[8];

		srcData[0] = context->frame->data[0] + scalerContext->source_top * context->frame->linesize[0] + scalerContext->source_left;
		srcData[1] = context->frame->data[1] + (scalerContext->source_top >> y_shift) * context->frame->linesize[1] + (scalerContext->source_left >> x_shift);
		srcData[2] = context->frame->data[2] + (scalerContext->source_top >> y_shift) * context->frame->linesize[2] + (scalerContext->source_left >> x_shift);
		srcData[3] = nullptr;
		srcData[4] = nullptr;
		srcData[5] = nullptr;
		srcData[6] = nullptr;
		srcData[7] = nullptr;

		sws_scale(scalerContext->sws_context, srcData, context->frame->linesize, 0,
			scalerContext->source_height, reinterpret_cast<uint8_t **>(&scaledBuffer), &scaledBufferStride);
	}
	else
	{
		sws_scale(scalerContext->sws_context, context->frame->data, context->frame->linesize, 0,
			scalerContext->source_height, reinterpret_cast<uint8_t **>(&scaledBuffer), &scaledBufferStride);
	}

	return 0;
}

void remove_video_decoder(void *handle)
{
	if (!handle)
		return;

	auto context = static_cast<VideoDecoderContext *>(handle);
	
	if (context->av_codec_context)
	{
		av_free(context->av_codec_context->extradata);
		avcodec_close(context->av_codec_context);
		av_free(context->av_codec_context);
	}

	av_frame_free(&context->frame);
	av_free(context);
}

int create_video_scaler(int sourceLeft, int sourceTop, int sourceWidth, int sourceHeight, int sourcePixelFormat,
	int scaledWidth, int scaledHeight, int scaledPixelFormat, int quality, void **handle)
{
	if (!handle)
		return -1;

	auto context = static_cast<ScalerContext *>(av_mallocz(sizeof(ScalerContext)));

	if (!context)
		return -2;

	const auto sourceAvPixelFormat = static_cast<AVPixelFormat>(sourcePixelFormat);
	const auto scaledAvPixelFormat = static_cast<AVPixelFormat>(scaledPixelFormat);

	SwsContext *swsContext = sws_getContext(sourceWidth, sourceHeight, sourceAvPixelFormat, scaledWidth, scaledHeight,
		scaledAvPixelFormat, quality, nullptr, nullptr, nullptr);
	
	if (!swsContext)
	{
		remove_video_scaler(context);
		return -3;
	}

	context->sws_context = swsContext;
	context->source_left = sourceLeft;
	context->source_top = sourceTop;
	context->source_height = sourceHeight;
	context->source_pixel_format = sourceAvPixelFormat;
	context->scaled_width = scaledWidth;
	context->scaled_height = scaledHeight;
	context->scaled_pixel_format = scaledAvPixelFormat;

	*handle = context;
	return 0;
}

void remove_video_scaler(void *handle)
{
	if (!handle)
		return;

	const auto context = static_cast<ScalerContext *>(handle);

	sws_freeContext(context->sws_context);
	av_free(context);
}

