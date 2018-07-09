using System;
using RtspClientSharp.Rtsp;

namespace RtspClientSharp.Utils
{
    static class ConnectionParametersExtensions
    {
        public static Uri GetFixedRtspUri(this ConnectionParameters connectionParameters)
        {
            var uriBuilder = new UriBuilder(connectionParameters.ConnectionUri)
            {
                Scheme = "rtsp"
            };

            if (connectionParameters.ConnectionUri.Port == -1)
                uriBuilder.Port = Constants.DefaultRtspPort;

            return uriBuilder.Uri;
        }
    }
}