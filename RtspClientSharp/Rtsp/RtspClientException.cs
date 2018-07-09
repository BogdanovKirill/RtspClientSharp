using System;
using System.Runtime.Serialization;

namespace RtspClientSharp.Rtsp
{
    [Serializable]
    public class RtspClientException : Exception
    {
        public RtspClientException()
        {
        }

        public RtspClientException(string message) : base(message)
        {
        }

        public RtspClientException(string message, Exception inner) : base(message, inner)
        {
        }

        protected RtspClientException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}