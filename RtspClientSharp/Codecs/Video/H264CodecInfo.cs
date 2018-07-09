using System;

namespace RtspClientSharp.Codecs.Video
{
    class H264CodecInfo : VideoCodecInfo
    {
        public byte[] SpsPpsBytes { get; set; } = Array.Empty<byte>();
    }
}