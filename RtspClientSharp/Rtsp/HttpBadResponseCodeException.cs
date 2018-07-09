using System;
using System.Net;
using System.Runtime.Serialization;

namespace RtspClientSharp.Rtsp
{
    [Serializable]
    public class HttpBadResponseCodeException : Exception
    {
        public HttpStatusCode Code { get; }

        public HttpBadResponseCodeException(HttpStatusCode code)
            : base($"Bad response code: {code}")
        {
            Code = code;
        }

        protected HttpBadResponseCodeException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}