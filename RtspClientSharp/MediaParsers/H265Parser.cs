using RtspClientSharp.RawFrames.Video;
using System;
using System.Collections.Generic;
using System.Text;

namespace RtspClientSharp.MediaParsers
{
    class H265Parser
    {
        public static readonly ArraySegment<byte> StartMarkerSegment = new ArraySegment<byte>(RawH265Frame.StartMarker);
    }
}
