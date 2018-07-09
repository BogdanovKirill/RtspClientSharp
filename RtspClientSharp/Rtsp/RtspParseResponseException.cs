using System;

namespace RtspClientSharp.Rtsp
{
    [Serializable]
    public class RtspParseResponseException : RtspClientException
    {
        public RtspParseResponseException(string message) : base(message)
        {
        }
    }
}