using System;
using SimpleRtspPlayer.RawFramesDecoding.DecodedFrames;

namespace SimpleRtspPlayer.GUI
{
    interface IAudioSource
    {
        event EventHandler<IDecodedAudioFrame> FrameReceived;
    }
}
