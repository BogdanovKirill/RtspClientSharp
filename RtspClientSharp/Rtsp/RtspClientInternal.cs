using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
    sealed class RtspClientInternal : IDisposable
    {
        private const int RtcpReportIntervalBaseMs = 5000;

        private readonly ConnectionParameters _connectionParameters;
        private readonly Func<IRtspTransportClient> _transportClientProvider;
        private readonly RtspRequestMessageFactory _requestMessageFactory;

        private readonly Dictionary<int, ITransportStream> _streamsMap = new Dictionary<int, ITransportStream>();
        private readonly ConcurrentDictionary<int, Socket> _udpClientsMap = new ConcurrentDictionary<int, Socket>();

        private readonly Dictionary<int, RtcpReceiverReportsProvider> _reportProvidersMap =
            new Dictionary<int, RtcpReceiverReportsProvider>();

        private TpktStream _tpktStream;

        private readonly SimpleHybridLock _hybridLock = new SimpleHybridLock();
        private readonly Random _random = RandomGeneratorFactory.CreateGenerator();
        private IRtspTransportClient _rtspTransportClient;

        private int _rtspKeepAliveTimeoutMs;

        private readonly CancellationTokenSource _serverCancellationTokenSource = new CancellationTokenSource();
        private bool _isServerSupportsGetParameterRequest;
        private int _disposed;

        public Action<RawFrame> FrameReceived;

        public RtspClientInternal(ConnectionParameters connectionParameters,
            Func<IRtspTransportClient> transportClientProvider = null)
        {
            _connectionParameters =
                connectionParameters ?? throw new ArgumentNullException(nameof(connectionParameters));
            _transportClientProvider = transportClientProvider ?? CreateTransportClient;

            Uri fixedRtspUri = connectionParameters.GetFixedRtspUri();
            _requestMessageFactory = new RtspRequestMessageFactory(fixedRtspUri, connectionParameters.UserAgent);
        }

        public async Task ConnectAsync(CancellationToken token)
        {
            IRtspTransportClient rtspTransportClient = _transportClientProvider();
            Volatile.Write(ref _rtspTransportClient, rtspTransportClient);

            await _rtspTransportClient.ConnectAsync(token);

            RtspRequestMessage optionsRequest = _requestMessageFactory.CreateOptionsRequest();
            RtspResponseMessage optionsResponse = await _rtspTransportClient.ExecuteRequest(optionsRequest, token);

            if (optionsResponse.StatusCode == RtspStatusCode.Ok)
                ParsePublicHeader(optionsResponse.Headers[WellKnownHeaders.Public]);

            RtspRequestMessage describeRequest = _requestMessageFactory.CreateDescribeRequest();
            RtspResponseMessage describeResponse =
                await _rtspTransportClient.EnsureExecuteRequest(describeRequest, token);

            string contentBaseHeader = describeResponse.Headers[WellKnownHeaders.ContentBase];

            if (!string.IsNullOrEmpty(contentBaseHeader))
                _requestMessageFactory.ContentBase = new Uri(contentBaseHeader);

            var parser = new SdpParser();
            IEnumerable<RtspTrackInfo> tracks = parser.Parse(describeResponse.ResponseBody);

            bool anyTrackRequested = false;
            foreach (RtspMediaTrackInfo track in GetTracksToSetup(tracks))
            {
                await SetupTrackAsync(track, token);
                anyTrackRequested = true;
            }

            if (!anyTrackRequested)
                throw new RtspClientException("Any suitable track is not found");

            RtspRequestMessage playRequest = _requestMessageFactory.CreatePlayRequest();
            await _rtspTransportClient.EnsureExecuteRequest(playRequest, token, 1);
        }

        public async Task ReceiveAsync(CancellationToken token)
        {
            if (_rtspTransportClient == null)
                throw new InvalidOperationException("Client should be connected first");

            TimeSpan nextRtspKeepAliveInterval = GetNextRtspKeepAliveInterval();

            using (var linkedTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(_serverCancellationTokenSource.Token, token))
            {
                CancellationToken linkedToken = linkedTokenSource.Token;

                Task receiveTask = _connectionParameters.RtpTransport == RtpTransportProtocol.TCP
                    ? ReceiveOverTcpAsync(_rtspTransportClient.GetStream(), linkedToken)
                    : ReceiveOverUdpAsync(linkedToken);

                if (!_isServerSupportsGetParameterRequest)
                    await receiveTask;
                else
                {
                    Task rtspKeepAliveDelayTask = Task.Delay(nextRtspKeepAliveInterval, linkedToken);

                    while (true)
                    {
                        Task result = await Task.WhenAny(receiveTask, rtspKeepAliveDelayTask);

                        if (result == receiveTask || result.IsCanceled)
                        {
                            await receiveTask;
                            break;
                        }

                        nextRtspKeepAliveInterval = GetNextRtspKeepAliveInterval();
                        rtspKeepAliveDelayTask = Task.Delay(nextRtspKeepAliveInterval, linkedToken);

                        await SendRtspKeepAliveAsync(linkedToken);
                    }
                }

                if (linkedToken.IsCancellationRequested)
                    await CloseRtspSessionAsync(CancellationToken.None);
            }
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            if (_udpClientsMap.Count != 0)
                foreach (Socket client in _udpClientsMap.Values)
                    client.Close();

            IRtspTransportClient rtspTransportClient = Volatile.Read(ref _rtspTransportClient);

            if (rtspTransportClient != null)
                _rtspTransportClient.Dispose();
        }

        private IRtspTransportClient CreateTransportClient()
        {
            if (_connectionParameters.ConnectionUri.Scheme.Equals(Uri.UriSchemeHttp,
                StringComparison.InvariantCultureIgnoreCase))
                return new RtspHttpTransportClient(_connectionParameters);

            return new RtspTcpTransportClient(_connectionParameters);
        }

        private TimeSpan GetNextRtspKeepAliveInterval()
        {
            return TimeSpan.FromMilliseconds(_random.Next(_rtspKeepAliveTimeoutMs / 2,
                _rtspKeepAliveTimeoutMs * 3 / 4));
        }

        private int GetNextRtcpReportIntervalMs()
        {
            return RtcpReportIntervalBaseMs + _random.Next(0, 11) * 100;
        }

        private async Task SetupTrackAsync(RtspMediaTrackInfo track, CancellationToken token)
        {
            RtspRequestMessage setupRequest;
            RtspResponseMessage setupResponse;

            int rtpChannelNumber;
            int rtcpChannelNumber;
            Socket rtpClient = null;
            Socket rtcpClient = null;

            if (_connectionParameters.RtpTransport == RtpTransportProtocol.UDP)
            {
                rtpClient = NetworkClientFactory.CreateUdpClient();
                rtcpClient = NetworkClientFactory.CreateUdpClient();

                try
                {
                    IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
                    rtpClient.Bind(endPoint);

                    int rtpPort = ((IPEndPoint) rtpClient.LocalEndPoint).Port;

                    endPoint = new IPEndPoint(IPAddress.Any, rtpPort + 1);

                    try
                    {
                        rtcpClient.Bind(endPoint);
                    }
                    catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    {
                        endPoint = new IPEndPoint(IPAddress.Any, 0);
                        rtcpClient.Bind(endPoint);
                    }

                    int rtcpPort = ((IPEndPoint) rtcpClient.LocalEndPoint).Port;

                    setupRequest = _requestMessageFactory.CreateSetupUdpUnicastRequest(track.TrackName,
                        rtpPort, rtcpPort);
                    setupResponse = await _rtspTransportClient.EnsureExecuteRequest(setupRequest, token);
                }
                catch
                {
                    rtpClient.Close();
                    rtcpClient.Close();
                    throw;
                }
            }
            else
            {
                int channelCounter = _streamsMap.Count;
                rtpChannelNumber = channelCounter;
                rtcpChannelNumber = ++channelCounter;

                setupRequest = _requestMessageFactory.CreateSetupTcpInterleavedRequest(track.TrackName,
                    rtpChannelNumber, rtcpChannelNumber);
                setupResponse = await _rtspTransportClient.EnsureExecuteRequest(setupRequest, token);
            }

            string transportHeader = setupResponse.Headers[WellKnownHeaders.Transport];

            if (string.IsNullOrEmpty(transportHeader))
                throw new RtspBadResponseException("Transport header is not found");

            string attributeName = _connectionParameters.RtpTransport == RtpTransportProtocol.UDP
                ? "server_port"
                : "interleaved";

            if (!ParseSeverPorts(transportHeader, attributeName, out rtpChannelNumber, out rtcpChannelNumber))
                throw new RtspBadResponseException("Server ports are not found");

            if (_connectionParameters.RtpTransport == RtpTransportProtocol.UDP)
            {
                IPEndPoint remoteEndPoint = (IPEndPoint) _rtspTransportClient.RemoteEndPoint;

                rtpClient?.Connect(new IPEndPoint(remoteEndPoint.Address, rtpChannelNumber));
                rtcpClient?.Connect(new IPEndPoint(remoteEndPoint.Address, rtcpChannelNumber));

                var udpHolePunchingPacketSegment = new ArraySegment<byte>(Array.Empty<byte>());

                await rtpClient.SendAsync(udpHolePunchingPacketSegment, SocketFlags.None);
                await rtcpClient.SendAsync(udpHolePunchingPacketSegment, SocketFlags.None);

                _udpClientsMap[rtpChannelNumber] = rtpClient;
                _udpClientsMap[rtcpChannelNumber] = rtcpClient;
            }

            ParseSessionHeader(setupResponse.Headers[WellKnownHeaders.Session]);

            IMediaPayloadParser mediaPayloadParser = MediaPayloadParser.CreateFrom(track.Codec);

            IRtpSequenceAssembler rtpSequenceAssembler;

            if (_connectionParameters.RtpTransport == RtpTransportProtocol.TCP)
            {
                rtpSequenceAssembler = null;
                mediaPayloadParser.FrameGenerated = OnFrameGeneratedLockfree;
            }
            else
            {
                rtpSequenceAssembler = new RtpSequenceAssembler(Constants.UdpReceiveBufferSize, 8);
                mediaPayloadParser.FrameGenerated = OnFrameGeneratedThreadSafe;
            }

            var rtpStream = new RtpStream(mediaPayloadParser, track.SamplesFrequency, rtpSequenceAssembler);
            _streamsMap.Add(rtpChannelNumber, rtpStream);

            var rtcpStream = new RtcpStream();
            rtcpStream.SessionShutdown += (sender, args) => _serverCancellationTokenSource.Cancel();
            _streamsMap.Add(rtcpChannelNumber, rtcpStream);

            uint senderSyncSourceId = (uint) _random.Next();

            var rtcpReportsProvider = new RtcpReceiverReportsProvider(rtpStream, rtcpStream, senderSyncSourceId);
            _reportProvidersMap.Add(rtpChannelNumber, rtcpReportsProvider);
        }

        private async Task SendRtspKeepAliveAsync(CancellationToken token)
        {
            RtspRequestMessage getParameterRequest = _requestMessageFactory.CreateGetParameterRequest();

            if (_connectionParameters.RtpTransport == RtpTransportProtocol.TCP)
                await _rtspTransportClient.SendRequestAsync(getParameterRequest, token);
            else
                await _rtspTransportClient.EnsureExecuteRequest(getParameterRequest, token);
        }

        private async Task CloseRtspSessionAsync(CancellationToken token)
        {
            RtspRequestMessage teardownRequest = _requestMessageFactory.CreateTeardownRequest();

            if (_connectionParameters.RtpTransport == RtpTransportProtocol.TCP)
                await _rtspTransportClient.SendRequestAsync(teardownRequest, token);
            else
                await _rtspTransportClient.EnsureExecuteRequest(teardownRequest, token);
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

        private bool ParseSeverPorts(string transportHeader, string attributeName, out int rtpPort, out int rtcpPort)
        {
            rtpPort = 0;
            rtcpPort = 0;

            int attributeStartIndex =
                transportHeader.IndexOf(attributeName, StringComparison.InvariantCultureIgnoreCase);

            if (attributeStartIndex == -1)
                return false;

            attributeStartIndex += attributeName.Length;

            int equalSignIndex = transportHeader.IndexOf('=', attributeStartIndex);

            if (equalSignIndex == -1)
                return false;

            int rtpPortStartIndex = ++equalSignIndex;

            if (rtpPortStartIndex == transportHeader.Length)
                return false;

            while (transportHeader[rtpPortStartIndex] == ' ')
                if (++rtpPortStartIndex == transportHeader.Length)
                    return false;

            int hyphenIndex = transportHeader.IndexOf('-', equalSignIndex);

            if (hyphenIndex == -1)
                return false;

            string rtpPortValue = transportHeader.Substring(rtpPortStartIndex, hyphenIndex - rtpPortStartIndex);

            if (!int.TryParse(rtpPortValue, out rtpPort))
                return false;

            int rtcpPortStartIndex = ++hyphenIndex;

            if (rtcpPortStartIndex == transportHeader.Length)
                return false;

            int rtcpPortEndIndex = rtcpPortStartIndex;

            while (transportHeader[rtcpPortEndIndex] != ';')
                if (++rtcpPortEndIndex == transportHeader.Length)
                    break;

            string rtcpPortValue = transportHeader.Substring(rtcpPortStartIndex, rtcpPortEndIndex - rtcpPortStartIndex);

            return int.TryParse(rtcpPortValue, out rtcpPort);
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

        private void OnFrameGeneratedLockfree(RawFrame frame)
        {
            FrameReceived?.Invoke(frame);
        }

        private void OnFrameGeneratedThreadSafe(RawFrame frame)
        {
            if (FrameReceived == null)
                return;

            _hybridLock.Enter();

            try
            {
                FrameReceived.Invoke(frame);
            }
            finally
            {
                _hybridLock.Leave();
            }
        }

        private async Task ReceiveOverTcpAsync(Stream rtspStream, CancellationToken token)
        {
            _tpktStream = new TpktStream(rtspStream);

            int nextRtcpReportInterval = GetNextRtcpReportIntervalMs();
            int lastTimeRtcpReportsSent = Environment.TickCount;
            var bufferStream = new MemoryStream();

            while (!token.IsCancellationRequested)
            {
                TpktPayload payload = await _tpktStream.ReadAsync();

                if (_streamsMap.TryGetValue(payload.Channel, out ITransportStream stream))
                    stream.Process(payload.PayloadSegment);

                int ticksNow = Environment.TickCount;
                if (TimeUtils.IsTimeOver(ticksNow, lastTimeRtcpReportsSent, nextRtcpReportInterval))
                {
                    lastTimeRtcpReportsSent = ticksNow;
                    nextRtcpReportInterval = GetNextRtcpReportIntervalMs();

                    foreach (KeyValuePair<int, RtcpReceiverReportsProvider> pair in _reportProvidersMap)
                    {
                        IEnumerable<RtcpPacket> packets = pair.Value.GetReportPackets();
                        ArraySegment<byte> byteSegment = SerializeRtcpPackets(packets, bufferStream);
                        int rtcpChannel = pair.Key + 1;

                        await _tpktStream.WriteAsync(rtcpChannel, byteSegment);
                    }
                }
            }
        }

        private Task ReceiveOverUdpAsync(CancellationToken token)
        {
            var waitList = new List<Task>(_udpClientsMap.Count / 2);

            foreach (KeyValuePair<int, Socket> pair in _udpClientsMap)
            {
                int channelNumber = pair.Key;
                Socket client = pair.Value;

                ITransportStream transportStream = _streamsMap[channelNumber];

                if (transportStream is RtpStream rtpStream)
                {
                    RtcpReceiverReportsProvider receiverReportsProvider = _reportProvidersMap[channelNumber];

                    Task receiveTask = ReceiveRtpFromUdpAsync(client, rtpStream, receiverReportsProvider, token);
                    waitList.Add(receiveTask);
                }
                else
                    ReceiveRtcpFromUdpAsync(client, transportStream, token).IgnoreExceptions();
            }

            return Task.WhenAll(waitList);
        }

        private async Task ReceiveRtpFromUdpAsync(Socket client, RtpStream rtpStream,
            RtcpReceiverReportsProvider reportsProvider,
            CancellationToken token)
        {
            var readBuffer = new byte[Constants.UdpReceiveBufferSize];
            var bufferSegment = new ArraySegment<byte>(readBuffer);

            int nextRtcpReportInterval = GetNextRtcpReportIntervalMs();
            int lastTimeRtcpReportsSent = Environment.TickCount;
            var bufferStream = new MemoryStream();

            while (!token.IsCancellationRequested)
            {
                int read = await client.ReceiveAsync(bufferSegment, SocketFlags.None);

                var payloadSegment = new ArraySegment<byte>(readBuffer, 0, read);
                rtpStream.Process(payloadSegment);

                int ticksNow = Environment.TickCount;
                if (TimeUtils.IsTimeOver(ticksNow, lastTimeRtcpReportsSent, nextRtcpReportInterval))
                {
                    lastTimeRtcpReportsSent = ticksNow;
                    nextRtcpReportInterval = GetNextRtcpReportIntervalMs();

                    IEnumerable<RtcpPacket> packets = reportsProvider.GetReportPackets();
                    ArraySegment<byte> byteSegment = SerializeRtcpPackets(packets, bufferStream);

                    await client.SendAsync(byteSegment, SocketFlags.None);
                }
            }
        }

        private static async Task ReceiveRtcpFromUdpAsync(Socket client, ITransportStream stream,
            CancellationToken token)
        {
            var readBuffer = new byte[Constants.UdpReceiveBufferSize];
            var bufferSegment = new ArraySegment<byte>(readBuffer);

            while (!token.IsCancellationRequested)
            {
                int read = await client.ReceiveAsync(bufferSegment, SocketFlags.None);

                var payloadSegment = new ArraySegment<byte>(readBuffer, 0, read);
                stream.Process(payloadSegment);
            }
        }

        private ArraySegment<byte> SerializeRtcpPackets(IEnumerable<RtcpPacket> packets, MemoryStream bufferStream)
        {
            bufferStream.Position = 0;

            foreach (ISerializablePacket report in packets.Cast<ISerializablePacket>())
                report.Serialize(bufferStream);

            byte[] streamBuffer = bufferStream.GetBuffer();
            return new ArraySegment<byte>(streamBuffer, 0, (int) bufferStream.Position);
        }
    }
}