using System;
using SimpleRtspPlayer.RawFramesDecoding.DecodedFrames;

namespace SimpleRtspPlayer.GUI
{
    public interface IVideoSource
    {
        event EventHandler<IDecodedVideoFrame> FrameReceived;

        void SetVideoSize(int width, int height);
    }
}