﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
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
        private static readonly char[] TransportAttributesSeparator = { ';' };
        private static readonly string ScaleSetupHeaderRtspResponse = "Scales=";
        private static readonly string ScalePlayRequestHeader = "Scale";

        private readonly ConnectionParameters _connectionParameters;
        private readonly Func<IRtspTransportClient> _transportClientProvider;
        private readonly RtspRequestMessageFactory _requestMessageFactory;

        private IMediaPayloadParser _mediaPayloadParser;

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
        private string _supportedScale;

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

        public async Task ConnectAsync(RtspRequestParams requestParams)
        {
            if (requestParams == null)
                throw new RtspClientException("Request parameters can't be null");

            IRtspTransportClient rtspTransportClient = _transportClientProvider();
            Volatile.Write(ref _rtspTransportClient, rtspTransportClient);

            await _rtspTransportClient.ConnectAsync(requestParams.Token);

            RtspRequestMessage optionsRequest = _requestMessageFactory.CreateOptionsRequest();
            RtspResponseMessage optionsResponse = await _rtspTransportClient.ExecuteRequest(optionsRequest, requestParams.Token);

            if (optionsResponse.StatusCode == RtspStatusCode.Ok)
                ParsePublicHeader(optionsResponse.Headers[WellKnownHeaders.Public]);

            RtspRequestMessage describeRequest = _requestMessageFactory.CreateDescribeRequest();
            RtspResponseMessage describeResponse =
                await _rtspTransportClient.EnsureExecuteRequest(describeRequest, requestParams.Token);

            string contentBaseHeader = describeResponse.Headers[WellKnownHeaders.ContentBase];

            if (!string.IsNullOrEmpty(contentBaseHeader))
                _requestMessageFactory.ContentBase = new Uri(contentBaseHeader);

            var parser = new SdpParser();
            IEnumerable<RtspTrackInfo> tracks = parser.Parse(describeResponse.ResponseBody);

            bool anyTrackRequested = false;

            foreach (RtspMediaTrackInfo track in GetTracksToSetup(tracks))
            {
                await SetupTrackAsync(requestParams.InitialTimestamp, track, requestParams.Token);
                anyTrackRequested = true;
            }

            if (!anyTrackRequested)
                throw new RtspClientException("Any suitable track is not found");

            if (!string.IsNullOrEmpty(_supportedScale) && IsScaleRequested(requestParams))
            {
                if (requestParams.Headers.TryGetValue(ScalePlayRequestHeader, out string scaleRequested))
                {
                    requestParams.Headers[ScalePlayRequestHeader] = GetScaleFromSupportedScalesBasedOnRequestedScale(scaleRequested);
                }
            }

            // TODO: Seems like some timestamps are being returned with 2 different timezones and/or some difference between the requested datetime and the returned one.
            RtspRequestMessage playRequest = requestParams.IsSetTimestampInClock ? _requestMessageFactory.CreatePlayRequest(requestParams) : _requestMessageFactory.CreatePlayRequest();
            RtspResponseMessage playResponse = await _rtspTransportClient.EnsureExecuteRequest(playRequest, requestParams.Token, 1);

            //// TODO : Create a specific parse to convert the clock values
            //Regex clockRegex = new Regex(@"clock=(?<startTime>\d{8}T\d{6}Z)\-(?<endTime>\d{8}T\d{6}Z)", RegexOptions.Singleline);
            //foreach (string playResponseHeader in playResponse.Headers.GetValues("Range"))
            //{
            //    Match clockMatches = clockRegex.Match(playResponseHeader);
            //    if (clockMatches.Success)
            //        _mediaPayloadParser.BaseTime = DateTime.ParseExact(clockMatches.Groups["startTime"].Value, "yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture, DateTimeStyles.None);
            //}
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

        private async Task SetupTrackAsync(DateTime? initialTimeStamp, RtspMediaTrackInfo track, CancellationToken token)
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

                    int rtpPort = ((IPEndPoint)rtpClient.LocalEndPoint).Port;

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

                    int rtcpPort = ((IPEndPoint)rtcpClient.LocalEndPoint).Port;

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

            string mediaPropertiesHeader = setupResponse.Headers[WellKnownHeaders.MediaProperties];
            if (!string.IsNullOrEmpty(mediaPropertiesHeader))
                GetSupportedScaleFromMediaProperties(mediaPropertiesHeader);

            string transportHeader = setupResponse.Headers[WellKnownHeaders.Transport];

            if (string.IsNullOrEmpty(transportHeader))
                throw new RtspBadResponseException("Transport header is not found");

            string portsAttributeName = _connectionParameters.RtpTransport == RtpTransportProtocol.UDP
                ? "server_port"
                : "interleaved";

            string[] transportAttributes = transportHeader.Split(TransportAttributesSeparator, StringSplitOptions.RemoveEmptyEntries);

            string portsAttribute = transportAttributes.FirstOrDefault(a => a.StartsWith(portsAttributeName, StringComparison.InvariantCultureIgnoreCase));

            if (portsAttribute == null || !TryParseSeverPorts(portsAttribute, out rtpChannelNumber, out rtcpChannelNumber))
                throw new RtspBadResponseException("Server ports are not found");

            if (_connectionParameters.RtpTransport == RtpTransportProtocol.UDP)
            {
                string sourceAttribute = transportAttributes.FirstOrDefault(a => a.StartsWith("source", StringComparison.InvariantCultureIgnoreCase));
                int equalSignIndex;

                IPAddress sourceAddress;

                if (sourceAttribute != null && (equalSignIndex = sourceAttribute.IndexOf("=", StringComparison.CurrentCultureIgnoreCase)) != -1)
                    sourceAddress = IPAddress.Parse(sourceAttribute.Substring(++equalSignIndex).Trim());
                else
                    sourceAddress = ((IPEndPoint)_rtspTransportClient.RemoteEndPoint).Address;

                Debug.Assert(rtpClient != null, nameof(rtpClient) + " != null");
                rtpClient.Connect(new IPEndPoint(sourceAddress, rtpChannelNumber));
                Debug.Assert(rtcpClient != null, nameof(rtcpClient) + " != null");
                rtcpClient.Connect(new IPEndPoint(sourceAddress, rtcpChannelNumber));

                var udpHolePunchingPacketSegment = new ArraySegment<byte>(Array.Empty<byte>());

                await rtpClient.SendAsync(udpHolePunchingPacketSegment, SocketFlags.None);
                await rtcpClient.SendAsync(udpHolePunchingPacketSegment, SocketFlags.None);

                _udpClientsMap[rtpChannelNumber] = rtpClient;
                _udpClientsMap[rtcpChannelNumber] = rtcpClient;
            }

            ParseSessionHeader(setupResponse.Headers[WellKnownHeaders.Session]);

            _mediaPayloadParser = MediaPayloadParser.CreateFrom(track.Codec);
            _mediaPayloadParser.BaseTime = (initialTimeStamp != null ? initialTimeStamp.Value : default(DateTime));

            IRtpSequenceAssembler rtpSequenceAssembler;

            if (_connectionParameters.RtpTransport == RtpTransportProtocol.TCP)
            {
                rtpSequenceAssembler = null;
                _mediaPayloadParser.FrameGenerated = OnFrameGeneratedLockfree;
            }
            else
            {
                rtpSequenceAssembler = new RtpSequenceAssembler(Constants.UdpReceiveBufferSize, 256);
                _mediaPayloadParser.FrameGenerated = OnFrameGeneratedThreadSafe;
            }

            var rtpStream = new RtpStream(_mediaPayloadParser, track.SamplesFrequency, rtpSequenceAssembler);
            _streamsMap.Add(rtpChannelNumber, rtpStream);

            var rtcpStream = new RtcpStream();
            rtcpStream.SessionShutdown += (sender, args) => _serverCancellationTokenSource.Cancel();
            _streamsMap.Add(rtcpChannelNumber, rtcpStream);

            uint senderSyncSourceId = (uint)_random.Next();

            var rtcpReportsProvider = new RtcpReceiverReportsProvider(rtpStream, rtcpStream, senderSyncSourceId);
            _reportProvidersMap.Add(rtpChannelNumber, rtcpReportsProvider);
        }

        private void GetSupportedScaleFromMediaProperties(string mediaPropertiesHeader)
        {
            string[] mediaProperties = Regex.Split(mediaPropertiesHeader, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");

            var scalesIndex = Array.FindIndex(mediaProperties, row => row.Contains(ScaleSetupHeaderRtspResponse));
            if (scalesIndex == -1)
                return;

            _supportedScale = mediaProperties.ElementAtOrDefault(scalesIndex).Replace("\"", string.Empty);
            //if (string.IsNullOrEmpty(scalesHeader))
            //    return;

            //var scales = scalesHeader.Split(',');
            //var lastScale = scales.ElementAtOrDefault(scales.Length - 1);
            //if (string.IsNullOrEmpty(lastScale))
            //    return;

            //string parsedScale = lastScale.Replace("\"", string.Empty);
            //if (int.TryParse(parsedScale, out int scaleNumber))
            //    _supportedScale = parsedScale;
        }

        private bool IsScaleRequested(RtspRequestParams requestParams)
        {
            if (requestParams.Headers == null)
                return false;

            return requestParams.Headers.ContainsKey(ScalePlayRequestHeader);
        }

        private string GetScaleFromSupportedScalesBasedOnRequestedScale(string referenceScale)
        {
            var scales = _supportedScale.Split(',');
            if (scales.Contains(referenceScale))
                return referenceScale;

            if (NumberUtils.IsNegativeNumber(referenceScale))
                return scales.First();
            else
                return scales.Last();
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

            _rtspKeepAliveTimeoutMs = (int)(timeout * 1000);
        }

        private bool TryParseSeverPorts(string portsAttribute, out int rtpPort, out int rtcpPort)
        {
            rtpPort = 0;
            rtcpPort = 0;

            int equalSignIndex = portsAttribute.IndexOf('=');

            if (equalSignIndex == -1)
                return false;

            int rtpPortStartIndex = ++equalSignIndex;

            if (rtpPortStartIndex == portsAttribute.Length)
                return false;

            while (portsAttribute[rtpPortStartIndex] == ' ')
                if (++rtpPortStartIndex == portsAttribute.Length)
                    return false;

            int hyphenIndex = portsAttribute.IndexOf('-', equalSignIndex);

            if (hyphenIndex == -1)
                return false;

            string rtpPortValue = portsAttribute.Substring(rtpPortStartIndex, hyphenIndex - rtpPortStartIndex);

            if (!int.TryParse(rtpPortValue, out rtpPort))
                return false;

            int rtcpPortStartIndex = ++hyphenIndex;

            if (rtcpPortStartIndex == portsAttribute.Length)
                return false;

            int rtcpPortEndIndex = rtcpPortStartIndex;

            while (portsAttribute[rtcpPortEndIndex] != ';')
                if (++rtcpPortEndIndex == portsAttribute.Length)
                    break;

            string rtcpPortValue = portsAttribute.Substring(rtcpPortStartIndex, rtcpPortEndIndex - rtcpPortStartIndex);

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

                if (!TimeUtils.IsTimeOver(ticksNow, lastTimeRtcpReportsSent, nextRtcpReportInterval))
                    continue;

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

        private Task ReceiveOverUdpAsync(CancellationToken token)
        {
            var waitList = new List<Task>(_udpClientsMap.Count / 2);

            foreach (KeyValuePair<int, Socket> pair in _udpClientsMap)
            {
                int channelNumber = pair.Key;
                Socket client = pair.Value;

                ITransportStream transportStream = _streamsMap[channelNumber];

                Task receiveTask;

                if (transportStream is RtpStream rtpStream)
                {
                    RtcpReceiverReportsProvider receiverReportsProvider = _reportProvidersMap[channelNumber];
                    receiveTask = ReceiveRtpFromUdpAsync(client, rtpStream, receiverReportsProvider, token);
                }
                else
                    receiveTask = ReceiveRtcpFromUdpAsync(client, transportStream, token);

                waitList.Add(receiveTask);
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
                if (!TimeUtils.IsTimeOver(ticksNow, lastTimeRtcpReportsSent, nextRtcpReportInterval))
                    continue;

                lastTimeRtcpReportsSent = ticksNow;
                nextRtcpReportInterval = GetNextRtcpReportIntervalMs();

                IEnumerable<RtcpPacket> packets = reportsProvider.GetReportPackets();
                ArraySegment<byte> byteSegment = SerializeRtcpPackets(packets, bufferStream);

                await client.SendAsync(byteSegment, SocketFlags.None);
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
            return new ArraySegment<byte>(streamBuffer, 0, (int)bufferStream.Position);
        }
    }
}