using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RtspClientSharp.Codecs.Audio;
using RtspClientSharp.Codecs.Video;
using RtspClientSharp.MediaParsers;
using RtspClientSharp.RawFrames;
using RtspClientSharp.Rtcp;
using RtspClientSharp.Rtp;
using RtspClientSharp.Sdp;
using RtspClientSharp.Tpkt;
using RtspClientSharp.Utils;

namespace RtspClientSharp.Rtsp
{
    class RtspClientInternal
    {
        private const int RtcpReportIntervalBaseMs = 3000;

        private readonly ConnectionParameters _connectionParameters;

        private readonly RtspRequestMessageFactory _requestMessageFactory;

        private readonly Dictionary<int, ITransportStream> _streamsMap = new Dictionary<int, ITransportStream>();

        private readonly Dictionary<int, RtcpReceiverReportsProvider> _reportProvidersMap =
            new Dictionary<int, RtcpReceiverReportsProvider>();

        private readonly MemoryStream _rtcpPacketsStream = new MemoryStream();
        private readonly Random _random = RandomGeneratorFactory.CreateGenerator();

        private int _rtspKeepAliveTimeoutMs;

        private bool _isConnectionClosedByServer;
        private bool _isServerSupportsGetParameterRequest;

        public Action ReadingContinues;
        public Action<RawFrame> FrameReceived;

        public RtspClientInternal(ConnectionParameters connectionParameters)
        {
            _connectionParameters = connectionParameters;

            Uri fixedRtspUri = connectionParameters.GetFixedRtspUri();
            _requestMessageFactory = new RtspRequestMessageFactory(fixedRtspUri, connectionParameters.UserAgent);
        }

        public async Task ConnectAsync(IRtspTransportClient rtspTransportClient, CancellationToken token)
        {
            ResetState();

            RtspRequestMessage optionsRequest = _requestMessageFactory.CreateOptionsRequest();
            RtspResponseMessage optionsResponse = await rtspTransportClient.ExecuteRequest(optionsRequest, token);

            if (optionsResponse.StatusCode == RtspStatusCode.Ok)
                ParsePublicHeader(optionsResponse.Headers[WellKnownHeaders.Public]);

            RtspRequestMessage describeRequest = _requestMessageFactory.CreateDescribeRequest();
            RtspResponseMessage describeResponse =
                await rtspTransportClient.EnsureExecuteRequest(describeRequest, token);

            string contentBaseHeader = describeResponse.Headers[WellKnownHeaders.ContentBase];

            if (!string.IsNullOrEmpty(contentBaseHeader))
                _requestMessageFactory.ContentBase = new Uri(contentBaseHeader);

            var parser = new SdpParser();
            IEnumerable<RtspTrackInfo> tracks = parser.Parse(describeResponse.ResponseBody);

            int channelCounter = 0;
            uint senderSyncSourceId = (uint) _random.Next();

            bool anyTrackRequested = false;
            foreach (RtspMediaTrackInfo track in GetTracksToSetup(tracks))
            {
                int rtpChannelNumber = ++channelCounter;
                int rtcpChannelNumber = ++channelCounter;

                RtspRequestMessage setupRequest = _requestMessageFactory.CreateSetupTcpInterleavedRequest(
                    track.TrackName, rtpChannelNumber, rtcpChannelNumber);

                RtspResponseMessage setupResponse = await rtspTransportClient.EnsureExecuteRequest(setupRequest, token);

                ParseSessionHeader(setupResponse.Headers[WellKnownHeaders.Session]);

                IMediaPayloadParser mediaPayloadParser = MediaPayloadParser.CreateFrom(track.Codec);
                mediaPayloadParser.FrameGenerated = FrameReceived;

                var rtpStream = new RtpStream(mediaPayloadParser, track.SamplesFrequency);
                _streamsMap.Add(rtpChannelNumber, rtpStream);

                var rtcpStream = new RtcpStream();
                rtcpStream.SessionShutdown += (sender, args) => _isConnectionClosedByServer = true;
                _streamsMap.Add(rtcpChannelNumber, rtcpStream);

                var rtcpReportsProvider = new RtcpReceiverReportsProvider(rtpStream, rtcpStream, senderSyncSourceId);
                _reportProvidersMap.Add(rtcpChannelNumber, rtcpReportsProvider);

                anyTrackRequested = true;
            }

            if (!anyTrackRequested)
                throw new RtspClientException("Any suitable track is not found");

            RtspRequestMessage playRequest = _requestMessageFactory.CreatePlayRequest();
            await rtspTransportClient.EnsureExecuteRequest(playRequest, token, 1);
        }

        public async Task ReceiveAsync(IRtspTransportClient rtspTransportClient, CancellationToken token)
        {
            var tpktStream = new TpktStream(rtspTransportClient.GetStream());

            int nextKeepAliveIntervalMs = GetNextRtspKeepAliveInterval();
            int rtcpReportIntervalMs = GetNextRtcpReportInterval();

            int ticksNow = Environment.TickCount;
            int lastTimeRtspKeepAliveSent = ticksNow;
            int lastTimeRtcpReportsPrepared = ticksNow;

            while (!_isConnectionClosedByServer && !token.IsCancellationRequested)
            {
                TpktPayload payload = await tpktStream.ReadAsync();

                if (_streamsMap.TryGetValue(payload.Channel, out ITransportStream channel))
                    channel.Process(payload.PayloadSegment);

                ReadingContinues?.Invoke();

                ticksNow = Environment.TickCount;

                if (TimeUtils.IsTimeOver(ticksNow, lastTimeRtcpReportsPrepared, rtcpReportIntervalMs))
                {
                    lastTimeRtcpReportsPrepared = ticksNow;
                    rtcpReportIntervalMs = GetNextRtcpReportInterval();

                    foreach (KeyValuePair<int, RtcpReceiverReportsProvider> pair in _reportProvidersMap)
                    {
                        IReadOnlyList<RtcpPacket> packets = pair.Value.GetReportPackets();

                        _rtcpPacketsStream.Position = 0;

                        foreach (ISerializablePacket report in packets.Cast<ISerializablePacket>())
                            report.Serialize(_rtcpPacketsStream);

                        var byteSegment = new ArraySegment<byte>(_rtcpPacketsStream.GetBuffer(), 0,
                            (int) _rtcpPacketsStream.Position);

                        await tpktStream.WriteAsync(pair.Key, byteSegment);
                    }
                }

                if (_isServerSupportsGetParameterRequest &&
                    TimeUtils.IsTimeOver(ticksNow, lastTimeRtspKeepAliveSent, nextKeepAliveIntervalMs))
                {
                    lastTimeRtspKeepAliveSent = ticksNow;
                    nextKeepAliveIntervalMs = GetNextRtspKeepAliveInterval();

                    RtspRequestMessage getParameterRequest = _requestMessageFactory.CreateGetParameterRequest();
                    await rtspTransportClient.SendRequestAsync(getParameterRequest);
                }
            }

            if (token.IsCancellationRequested)
            {
                RtspRequestMessage teardownRequest = _requestMessageFactory.CreateTeardownRequest();
                await rtspTransportClient.SendRequestAsync(teardownRequest);
            }
        }

        private void ResetState()
        {
            _requestMessageFactory.ResetState();
            _isConnectionClosedByServer = false;
            _isServerSupportsGetParameterRequest = false;
            _streamsMap.Clear();
            _reportProvidersMap.Clear();
        }

        private IEnumerable<RtspMediaTrackInfo> GetTracksToSetup(IEnumerable<RtspTrackInfo> tracks)
        {
            foreach (RtspMediaTrackInfo track in tracks.OfType<RtspMediaTrackInfo>())
            {
                if (track.Codec is VideoCodecInfo && (_connectionParameters.RequiredTracks & RequiredTracks.Video) != 0)
                    yield return track;
                else if (track.Codec is AudioCodecInfo &&
                         (_connectionParameters.RequiredTracks & RequiredTracks.Audio) != 0)
                    yield return track;
            }
        }

        private void ParsePublicHeader(string publicHeader)
        {
            if (!string.IsNullOrEmpty(publicHeader))
            {
                string getParameterName = RtspMethod.GET_PARAMETER.ToString();

                if (publicHeader.IndexOf(getParameterName, StringComparison.InvariantCulture) != -1)
                    _isServerSupportsGetParameterRequest = true;
            }
        }

        private void ParseSessionHeader(string sessionHeader)
        {
            uint timeout = 0;

            if (!string.IsNullOrEmpty(sessionHeader))
            {
                int delimiter = sessionHeader.IndexOf(';');

                if (delimiter != -1)
                {
                    TryParseTimeoutParameter(sessionHeader, out timeout);
                    _requestMessageFactory.SessionId = sessionHeader.Substring(0, delimiter);
                }
                else
                    _requestMessageFactory.SessionId = sessionHeader;
            }

            if (timeout == 0)
                timeout = 60;

            _rtspKeepAliveTimeoutMs = (int) (timeout * 1000);
        }

        private int GetNextRtspKeepAliveInterval()
        {
            return _random.Next(_rtspKeepAliveTimeoutMs / 2, _rtspKeepAliveTimeoutMs * 3 / 4);
        }

        private int GetNextRtcpReportInterval()
        {
            return RtcpReportIntervalBaseMs + _random.Next(0, 11) * 100;
        }

        private static void TryParseTimeoutParameter(string sessionHeader, out uint timeout)
        {
            const string timeoutParameterName = "timeout";

            timeout = 0;

            int delimiter = sessionHeader.IndexOf(';');

            if (delimiter == -1)
                return;

            int timeoutIndex = sessionHeader.IndexOf(timeoutParameterName, ++delimiter,
                StringComparison.InvariantCultureIgnoreCase);

            if (timeoutIndex == -1)
                return;

            timeoutIndex += timeoutParameterName.Length;

            int equalsSignIndex = sessionHeader.IndexOf('=', timeoutIndex);

            if (equalsSignIndex == -1)
                return;

            int valueStartPos = ++equalsSignIndex;

            if (valueStartPos == sessionHeader.Length)
                return;

            while (sessionHeader[valueStartPos] == ' ' || sessionHeader[valueStartPos] == '\"')
                if (++valueStartPos == sessionHeader.Length)
                    return;

            int valueEndPos = valueStartPos;

            while (sessionHeader[valueEndPos] >= '0' && sessionHeader[valueEndPos] <= '9')
                if (++valueEndPos == sessionHeader.Length)
                    break;

            string value = sessionHeader.Substring(valueStartPos, valueEndPos - valueStartPos);

            uint.TryParse(value, out timeout);
        }
    }
}