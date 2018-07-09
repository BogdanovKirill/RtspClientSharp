namespace RtspClientSharp.Codecs.Audio
{
    class AACCodecInfo : AudioCodecInfo
    {
        public byte[] ConfigBytes { get; set; }
        public int SizeLength { get; set; }
        public int IndexLength { get; set; }
        public int IndexDeltaLength { get; set; }
    }
}