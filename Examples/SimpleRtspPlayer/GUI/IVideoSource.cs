using System;
using SimpleRtspPlayer.RawFramesDecoding.DecodedFrames;

namespace SimpleRtspPlayer.GUI
{
    public interface IVideoSource
    {
        event EventHandler<IDecodedVideoFrame> FrameReceived;
    }
}