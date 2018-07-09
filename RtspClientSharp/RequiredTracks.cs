using System;

namespace RtspClientSharp
{
    [Flags]
    public enum RequiredTracks
    {
        Video = 1,
        Audio = 2,
        All = Video | Audio
    }
}