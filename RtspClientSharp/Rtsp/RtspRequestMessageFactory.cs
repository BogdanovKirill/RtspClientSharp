using System;

namespace RtspClientSharp.Rtsp
{
    class RtspRequestMessageFactory
    {
        private static readonly Version ProtocolVersion = new Version(1, 0);

        private uint _cSeq;
        private readonly Uri _rtspUri;
        private readonly string _userAgent;

        public Uri ContentBase { get; set; }
        public string SessionId { get; set; }

        public RtspRequestMessageFactory(Uri rtspUri, string userAgent)
        {
            _rtspUri = rtspUri ?? throw new ArgumentNullException(nameof(rtspUri));
            _userAgent = userAgent;
        }

        public RtspRequestMessage CreateOptionsRequest()
        {
            var rtspRequestMessage = new RtspRequestMessage(RtspMethod.OPTIONS, _rtspUri, ProtocolVersion, ++_cSeq,
                _userAgent, SessionId);
            return rtspRequestMessage;
        }

        public RtspRequestMessage CreateDescribeRequest()
        {
            var rtspRequestMessage = new RtspRequestMessage(RtspMethod.DESCRIBE, _rtspUri, ProtocolVersion, ++_cSeq,
                _userAgent, SessionId);
            rtspRequestMessage.Headers.Add("Accept", "application/sdp");
            return rtspRequestMessage;
        }

        public RtspRequestMessage CreateSetupTcpInterleavedRequest(string trackName, int rtpChannel, int rtcpChannel)
        {
            Uri trackUri = !Uri.IsWellFormedUriString(trackName, UriKind.Absolute)
                ? new Uri(GetContentBasedUri(), trackName)
                : new Uri(trackName, UriKind.Absolute);

            var rtspRequestMessage = new RtspRequestMessage(RtspMethod.SETUP, trackUri, ProtocolVersion, ++_cSeq,
                _userAgent, SessionId);
            rtspRequestMessage.Headers.Add("Transport", $"RTP/AVP/TCP;unicast;interleaved={rtpChannel}-{rtcpChannel}");
            return rtspRequestMessage;
        }

        public RtspRequestMessage CreatePlayRequest()
        {
            Uri uri = GetContentBasedUri();

            var rtspRequestMessage =
                new RtspRequestMessage(RtspMethod.PLAY, uri, ProtocolVersion, ++_cSeq, _userAgent, SessionId);
            rtspRequestMessage.Headers.Add("Range", "npt=0.000-");
            return rtspRequestMessage;
        }

        public RtspRequestMessage CreateTeardownRequest()
        {
            var rtspRequestMessage = new RtspRequestMessage(RtspMethod.TEARDOWN, _rtspUri, ProtocolVersion, ++_cSeq,
                _userAgent, SessionId);
            return rtspRequestMessage;
        }

        public RtspRequestMessage CreateGetParameterRequest()
        {
            var rtspRequestMessage = new RtspRequestMessage(RtspMethod.GET_PARAMETER, _rtspUri, ProtocolVersion,
                ++_cSeq, _userAgent, SessionId);
            return rtspRequestMessage;
        }

        public void ResetState()
        {
            _cSeq = 0;
            ContentBase = null;
            SessionId = null;
        }

        private Uri GetContentBasedUri()
        {
            if (ContentBase != null)
                return ContentBase;

            return _rtspUri;
        }
    }
}