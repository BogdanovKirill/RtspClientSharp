using System;

namespace RtspClientSharp.Rtsp
{
    [Serializable]
    public class RtspBadResponseException : RtspClientException
    {
        public RtspBadResponseException(string message) : base(message)
        {
        }
    }
}