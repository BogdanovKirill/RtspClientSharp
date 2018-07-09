#include "stdafx.h"

struct VideoDecoderContext
{
	AVCodec *Codec;
	AVCodecContext *AvCodecContext;
	AVPacket AvRawPacket;
	AVFrame *Frame;
};

struct ScalerContext
{
	SwsContext *SwsContext;
	int SourceLeft;
	int SourceTop;
	int SourceHeight;
	AVPixelFormat SourcePixelFormat;
	int ScaledWidth;
	int ScaledHeight;
	AVPixelFormat ScaledPixelFormat;
};

int create_video_decoder(int codec_id, void **handle)
{
	if (!handle)
		return -1;

	VideoDecoderContext *context = (VideoDecoderContext *)av_mallocz(sizeof(VideoDecoderContext));

	if (!context)
		return -2;

	context->Codec = avcodec_find_decoder((AVCodecID)codec_id);
	if (!context->Codec)
	{
		remove_video_decoder(context);
		return -3;
	}

	context->AvCodecContext = avcodec_alloc_context3(context->Codec);
	if (!context->AvCodecContext)
	{
		remove_video_decoder(context);
		return -4;
	}

	if (avcodec_open2(context->AvCodecContext, context->Codec, NULL) < 0)
	{
		remove_video_decoder(context);
		return -5;
	}

	context->Frame = av_frame_alloc();
	if (!context->Frame)
	{
		remove_video_decoder(context);
		return -6;
	}

	av_init_packet(&context->AvRawPacket);

	*handle = context;
	return 0;
}

int set_video_decoder_extradata(void *handle, void *extradata, int extradataLength)
{
#if _DEBUG
	if (!handle || !extradata || !extradataLength)
		return -1;
#endif

	VideoDecoderContext *context = (VideoDecoderContext *)handle;

	if (!context->AvCodecContext->extradata || context->AvCodecContext->extradata_size < extradataLength)
	{
		av_free(context->AvCodecContext->extradata);
		context->AvCodecContext->extradata = (uint8_t*)av_malloc(extradataLength + AV_INPUT_BUFFER_PADDING_SIZE);

		if (!context->AvCodecContext->extradata)
			return -2;
	}

	context->AvCodecContext->extradata_size = extradataLength;

	memcpy(context->AvCodecContext->extradata, extradata, extradataLength);
	memset(context->AvCodecContext->extradata + extradataLength, 0, AV_INPUT_BUFFER_PADDING_SIZE);
	
	avcodec_close(context->AvCodecContext);
	if (avcodec_open2(context->AvCodecContext, context->Codec, NULL) < 0)
		return -3;

	return 0;
}

int decode_video_frame(void *handle, void *rawBuffer, int rawBufferLength, int *frameWidth, int *frameHeight, int *framePixelFormat)
{
#if _DEBUG
	if (!handle || !rawBuffer || !rawBufferLength || !frameWidth || !frameHeight || !framePixelFormat)
		return -1;

	if ((uintptr_t)rawBuffer % 4 != 0)
		return -2;
#endif

	VideoDecoderContext *context = (VideoDecoderContext *)handle;

	context->AvRawPacket.data = (uint8_t *)rawBuffer;
	context->AvRawPacket.size = rawBufferLength;

	int got_frame;

	int len = avcodec_decode_video2(context->AvCodecContext, context->Frame, &got_frame, &context->AvRawPacket);

	if (len != rawBufferLength)
		return -3;

	if (got_frame)
	{
		*frameWidth = context->AvCodecContext->width;
		*frameHeight = context->AvCodecContext->height;
		*framePixelFormat = context->AvCodecContext->pix_fmt;
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

	VideoDecoderContext *context = (VideoDecoderContext *)handle;
	ScalerContext *scalerContext = (ScalerContext *)scalerHandle;

	uint8_t *srcData[8];

	if (scalerContext->SourceTop != 0 || scalerContext->SourceLeft != 0)
	{
		const AVPixFmtDescriptor *sourceFmtDesc = av_pix_fmt_desc_get(scalerContext->SourcePixelFormat);

		if (!sourceFmtDesc)
			return -4;

		int x_shift = sourceFmtDesc->log2_chroma_w;
		int y_shift = sourceFmtDesc->log2_chroma_h;

		srcData[0] = context->Frame->data[0] + scalerContext->SourceTop * context->Frame->linesize[0] + scalerContext->SourceLeft;
		srcData[1] = context->Frame->data[1] + (scalerContext->SourceTop >> y_shift) * context->Frame->linesize[1] + (scalerContext->SourceLeft >> x_shift);
		srcData[2] = context->Frame->data[2] + (scalerContext->SourceTop >> y_shift) * context->Frame->linesize[2] + (scalerContext->SourceLeft >> x_shift);
		srcData[3] = 0;
		srcData[4] = 0;
		srcData[5] = 0;
		srcData[6] = 0;
		srcData[7] = 0;
	}
	else
		memcpy(srcData, context->Frame->data, sizeof(srcData));

	sws_scale(scalerContext->SwsContext, srcData, context->Frame->linesize, 0, scalerContext->SourceHeight, (uint8_t **)&scaledBuffer, &scaledBufferStride);
	return 0;
}

void remove_video_decoder(void *handle)
{
	if (!handle)
		return;

	VideoDecoderContext *context = (VideoDecoderContext *)handle;
	
	if (context->AvCodecContext)
	{
		av_free(context->AvCodecContext->extradata);
		avcodec_close(context->AvCodecContext);
		av_free(context->AvCodecContext);
	}

	av_frame_free(&context->Frame);
	av_free(context);
}

int create_video_scaler(int sourceLeft, int sourceTop, int sourceWidth, int sourceHeight, int sourcePixelFormat,
	int scaledWidth, int scaledHeight, int scaledPixelFormat, int quality, void **handle)
{
	if (!handle)
		return -1;

	ScalerContext *context = (ScalerContext *)av_mallocz(sizeof(ScalerContext));

	if (!context)
		return -2;

	AVPixelFormat sourceAvPixelFormat = static_cast<AVPixelFormat>(sourcePixelFormat);
	AVPixelFormat scaledAvPixelFormat = static_cast<AVPixelFormat>(scaledPixelFormat);

	SwsContext *swsContext = sws_getContext(sourceWidth, sourceHeight, sourceAvPixelFormat, scaledWidth, scaledHeight,
		scaledAvPixelFormat, quality, NULL, NULL, NULL);
	
	if (!swsContext)
	{
		remove_video_scaler(context);
		return -3;
	}

	context->SwsContext = swsContext;
	context->SourceLeft = sourceLeft;
	context->SourceTop = sourceTop;
	context->SourceHeight = sourceHeight;
	context->SourcePixelFormat = sourceAvPixelFormat;
	context->ScaledWidth = scaledWidth;
	context->ScaledHeight = scaledHeight;
	context->ScaledPixelFormat = scaledAvPixelFormat;

	*handle = context;
	return 0;
}

void remove_video_scaler(void *handle)
{
	if (!handle)
		return;

	ScalerContext *context = (ScalerContext *)av_mallocz(sizeof(ScalerContext));

	sws_freeContext(context->SwsContext);
	av_free(context);
}

