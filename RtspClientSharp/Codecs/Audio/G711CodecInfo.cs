namespace RtspClientSharp.Codecs.Audio
{
    abstract class G711CodecInfo : AudioCodecInfo
    {
        public int SampleRate { get; set; } = 8000;
        public int Channels { get; set; } = 1;
    }
}