using RtspClientSharp.Codecs;
using RtspClientSharp.Codecs.Audio;
using RtspClientSharp.Codecs.Data;
using RtspClientSharp.Codecs.Video;
using RtspClientSharp.RawFrames;
using System;

namespace RtspClientSharp.MediaParsers
{
    abstract class MediaPayloadParser : IMediaPayloadParser
    {
        public DateTime BaseTime { get; set; }

        public Action<RawFrame> FrameGenerated { get; set; }
        public Action<byte[]> NaluReceived { get; set; }


        public abstract void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit);

        public abstract void ResetState();

        protected DateTime GetFrameTimestamp(TimeSpan timeOffset)
        {
            if (BaseTime == default(DateTime))
                BaseTime = DateTime.UtcNow;

            if (timeOffset == TimeSpan.MinValue)
                return BaseTime;

            return BaseTime + timeOffset;
        }

        protected virtual void OnFrameGenerated(RawFrame e)
        {
            FrameGenerated?.Invoke(e);
        }

        public static IMediaPayloadParser CreateFrom(CodecInfo codecInfo, Action<byte[]> naluReceived)
        {
            switch (codecInfo)
            {
                case H264CodecInfo h264CodecInfo:
                    return new H264VideoPayloadParser(h264CodecInfo, naluReceived);
                case H265CodecInfo h265CodecInfo:
                    return new H265VideoPayloadParser(h265CodecInfo, naluReceived);
                case MJPEGCodecInfo _:
                    return new MJPEGVideoPayloadParser();
                case AACCodecInfo aacCodecInfo:
                    return new AACAudioPayloadParser(aacCodecInfo);
                case G711CodecInfo g711CodecInfo:
                    return new G711AudioPayloadParser(g711CodecInfo);
                case G726CodecInfo g726CodecInfo:
                    return new G726AudioPayloadParser(g726CodecInfo);
                case PCMCodecInfo pcmCodecInfo:
                    return new PCMAudioPayloadParser(pcmCodecInfo);
                case OnvifMetadataCodecInfo onvifMetadataCodecInfo:
                    return new OnvifMetadataPayloadParser(onvifMetadataCodecInfo);
                default:
                    throw new ArgumentOutOfRangeException(nameof(codecInfo),
                        $"Unsupported codec: {codecInfo.GetType().Name}");
            }
        }
    }
}