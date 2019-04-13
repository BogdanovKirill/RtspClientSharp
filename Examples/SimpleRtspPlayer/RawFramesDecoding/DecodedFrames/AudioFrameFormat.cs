namespace SimpleRtspPlayer.RawFramesDecoding.DecodedFrames
{
    public struct AudioFrameFormat
    {
        public int SampleRate { get; }
        public int BitPerSample { get; }
        public int Channels { get; }
        public int BytesPerSecond { get; }

        public AudioFrameFormat(int sampleRate, int bitPerSample, int channels)
        {
            SampleRate = sampleRate;
            BitPerSample = bitPerSample;
            Channels = channels;
            BytesPerSecond = sampleRate * bitPerSample / 8 * channels;
        }
    }
}
