using System;

namespace RtspClientSharp.RawFrames.Audio
{
    public class RawPCMFrame : RawAudioFrame
    {
        public int SampleRate { get; }

        public int BitsPerSample { get; }

        public int Channels { get; }

        public RawPCMFrame(DateTime timestamp, ArraySegment<byte> frameSegment, int sampleRate, int bitsPerSample,
            int channels)
            : base(timestamp, frameSegment)
        {
            SampleRate = sampleRate;
            BitsPerSample = bitsPerSample;
            Channels = channels;
        }
    }
}