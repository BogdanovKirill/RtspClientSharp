﻿using System;

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
            var rtspRequestMessage = new RtspRequestMessage(RtspMethod.OPTIONS, _rtspUri, ProtocolVersion, 
                NextCSeqProvider, _userAgent, SessionId);
            return rtspRequestMessage;
        }

        public RtspRequestMessage CreateDescribeRequest()
        {
            var rtspRequestMessage = new RtspRequestMessage(RtspMethod.DESCRIBE, _rtspUri, ProtocolVersion, 
                NextCSeqProvider, _userAgent, SessionId);
            rtspRequestMessage.Headers.Add("Accept", "application/sdp");
            return rtspRequestMessage;
        }

        public RtspRequestMessage CreateSetupTcpInterleavedRequest(string trackName, int rtpChannel, int rtcpChannel)
        {
            Uri trackUri = GetTrackUri(trackName);

            var rtspRequestMessage = new RtspRequestMessage(RtspMethod.SETUP, trackUri, ProtocolVersion, 
                NextCSeqProvider, _userAgent, SessionId);
            rtspRequestMessage.Headers.Add("Transport", $"RTP/AVP/TCP;unicast;interleaved={rtpChannel}-{rtcpChannel}");
            return rtspRequestMessage;
        }

        public RtspRequestMessage CreateSetupUdpUnicastRequest(string trackName, int rtpPort, int rtcpPort)
        {
            Uri trackUri = GetTrackUri(trackName);

            var rtspRequestMessage = new RtspRequestMessage(RtspMethod.SETUP, trackUri, ProtocolVersion, 
                NextCSeqProvider, _userAgent, SessionId);
            rtspRequestMessage.Headers.Add("Transport", $"RTP/AVP/UDP;unicast;client_port={rtpPort}-{rtcpPort}");
            return rtspRequestMessage;
        }

        public RtspRequestMessage CreateSetupUdpMulticastRequest(string trackName)
        {
            Uri trackUri = GetTrackUri(trackName);

            var rtspRequestMessage = new RtspRequestMessage(RtspMethod.SETUP, trackUri, ProtocolVersion,
                NextCSeqProvider, _userAgent, SessionId);
            rtspRequestMessage.Headers.Add("Transport", $"RTP/AVP;multicast");
            return rtspRequestMessage;
        }

        public RtspRequestMessage CreatePlayRequest()
        {
            Uri uri = GetContentBasedUri();

            var rtspRequestMessage =
                new RtspRequestMessage(RtspMethod.PLAY, uri, ProtocolVersion, NextCSeqProvider, _userAgent, SessionId);
            rtspRequestMessage.Headers.Add("Range", "npt=0.000-");
            return rtspRequestMessage;
        }

        public RtspRequestMessage CreateTeardownRequest()
        {
            var rtspRequestMessage = new RtspRequestMessage(RtspMethod.TEARDOWN, _rtspUri, ProtocolVersion, 
                NextCSeqProvider, _userAgent, SessionId);
            return rtspRequestMessage;
        }

        public RtspRequestMessage CreateGetParameterRequest()
        {
            var rtspRequestMessage = new RtspRequestMessage(RtspMethod.GET_PARAMETER, _rtspUri, ProtocolVersion,
                NextCSeqProvider, _userAgent, SessionId);
            return rtspRequestMessage;
        }

        private Uri GetContentBasedUri()
        {
            if (ContentBase != null)
                return ContentBase;

            return _rtspUri;
        }

        private uint NextCSeqProvider()
        {
            return ++_cSeq;
        }
        
        private Uri GetTrackUri(string trackName)
        {
            Uri trackUri;

            if (!Uri.IsWellFormedUriString(trackName, UriKind.Absolute))
            {
                var uriBuilder = new UriBuilder(GetContentBasedUri());

                bool trackNameStartsWithSlash = trackName.StartsWith("/");

                if (uriBuilder.Path.EndsWith("/"))
                    uriBuilder.Path += trackNameStartsWithSlash ? trackName.Substring(1) : trackName;
                else
                    uriBuilder.Path += trackNameStartsWithSlash ? trackName : "/" + trackName;

                trackUri = uriBuilder.Uri;
            }
            else
                trackUri = new Uri(trackName, UriKind.Absolute);

            return trackUri;
        }
    }
}