using System;
using System.Net;
using RtspClientSharp.Rtsp;

namespace RtspClientSharp
{
    public class ConnectionParameters
    {
        private const string DefaultUserAgent = "RtspClientSharp";
        private Uri _fixedRtspUri;

        /// <summary>
        /// Uri should start from "rtsp://" prefix for RTSP over TCP transport
        /// and from "http://" for RTSP over HTTP tunneling
        /// </summary>
        public Uri ConnectionUri { get; }

        /// <summary>
        /// Should be used to get only one video/audio track from device.
        /// Important notes: some devices won't connect in that case (SETUP request could not be processed)
        /// </summary>
        public RequiredTracks RequiredTracks { get; set; } = RequiredTracks.All;

        public NetworkCredential Credentials { get; }
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public TimeSpan CancelTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public string UserAgent { get; set; } = DefaultUserAgent;
        public RtpTransportProtocol RtpTransport { get; set; } = RtpTransportProtocol.TCP;

        public ConnectionParameters(Uri connectionUri)
        {
            ValidateUri(connectionUri);

            ConnectionUri = connectionUri;
            Credentials = GetNetworkCredentialsFromUri(connectionUri);
        }

        public ConnectionParameters(Uri connectionUri, NetworkCredential credentials)
        {
            ValidateUri(connectionUri);

            ConnectionUri = connectionUri;
            Credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        }

        internal Uri GetFixedRtspUri()
        {
            if (_fixedRtspUri != null)
                return _fixedRtspUri;

            var uriBuilder = new UriBuilder(ConnectionUri)
            {
                Scheme = "rtsp"
            };

            if (ConnectionUri.Port == -1)
                uriBuilder.Port = Constants.DefaultRtspPort;

            _fixedRtspUri = uriBuilder.Uri;
            return _fixedRtspUri;
        }

        private static void ValidateUri(Uri connectionUri)
        {
            if (connectionUri == null)
                throw new ArgumentNullException(nameof(connectionUri));
            if (!connectionUri.IsAbsoluteUri)
                throw new ArgumentException("Connection uri should be absolute", nameof(connectionUri));
        }

        private static NetworkCredential GetNetworkCredentialsFromUri(Uri connectionUri)
        {
            string userInfo = connectionUri.UserInfo;

            string login = null;
            string password = null;

            if (!string.IsNullOrEmpty(userInfo))
            {
                string[] tokens = userInfo.Split(':');

                if (tokens.Length == 2)
                {
                    login = tokens[0];
                    password = tokens[1];
                }
            }

            return new NetworkCredential(login, password);
        }
    }
}