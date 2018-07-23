using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RtspClientSharp.Rtsp;
using RtspClientSharp.Tpkt;

namespace RtspClientSharp.UnitTests.Rtsp
{
    class RtspTransportClientEmulator : IRtspTransportClient
    {
        private const string SdpDocument = "m=video 0 RTP/AVP 96\r\n" +
                                           "c=IN IP4 0.0.0.0\r\n" +
                                           "b=AS:50000\r\n" +
                                           "a=control:rtsp://127.0.0.1/media/video/track1\r\n" +
                                           "a=rtpmap:96 H264/90000\r\n" +
                                           "a=fmtp:96 packetization-mode=1";

        private static readonly Version ProtocolVersion = new Version(1, 0);

        private static readonly RtspResponseMessage OptionsResponse = new RtspResponseMessage(RtspStatusCode.Ok,
            ProtocolVersion, 1,
            new NameValueCollection
            {
                {"PUBLIC", "DESCRIBE, GET_PARAMETER, PAUSE, PLAY, SETUP, SET_PARAMETER, TEARDOWN"}
            });

        private static readonly RtspResponseMessage DescribeResponse = new RtspResponseMessage(RtspStatusCode.Ok,
            ProtocolVersion, 2,
            new NameValueCollection
            {
                {"CONTENT-TYPE", "application/sdp"},
                {"CONTENT-BASE", "rtsp://127.0.0.1/media/video/"},
                {"CONTENT-LENGTH", "616"}
            })
        {
            ResponseBody = Encoding.ASCII.GetBytes(SdpDocument)
        };

        private static readonly RtspResponseMessage SetupInterleavedResponse = new RtspResponseMessage(
            RtspStatusCode.Ok, ProtocolVersion, 3,
            new NameValueCollection
            {
                {"SESSION", "984686A7;timeout=60"},
                {"TRANSPORT", "RTP/AVP/TCP;unicast;interleaved=1-2;ssrc=8D50905E;mode=\"PLAY\""}
            });

        private static readonly RtspResponseMessage PlayResponse = new RtspResponseMessage(RtspStatusCode.Ok,
            ProtocolVersion, 4,
            new NameValueCollection());

        private static readonly byte[] RtcpInterleavedByePacketsBytes =
            {TpktHeader.Id, 0x02, 0x00, 0x04, 0x80, 0xCB, 0x00, 0x00};

        public EndPoint RemoteEndPoint => new IPEndPoint(IPAddress.Loopback, 11080);

        public virtual Task ConnectAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public virtual Stream GetStream()
        {
            var ms = new MemoryStream();
            ms.Write(RtcpInterleavedByePacketsBytes, 0, RtcpInterleavedByePacketsBytes.Length);
            ms.Position = 0;
            return ms;
        }

        public void Dispose()
        {
        }

        public Task<RtspResponseMessage> EnsureExecuteRequest(RtspRequestMessage requestMessage,
            CancellationToken token, int responseReadPortionSize = 0)
        {
            return ExecuteRequest(requestMessage, token, responseReadPortionSize);
        }

        public Task<RtspResponseMessage> ExecuteRequest(RtspRequestMessage requestMessage, CancellationToken token,
            int responseReadPortionSize = 0)
        {
            token.ThrowIfCancellationRequested();

            switch (requestMessage.Method)
            {
                case RtspMethod.OPTIONS:
                    return Task.FromResult(OptionsResponse);
                case RtspMethod.DESCRIBE:
                    return Task.FromResult(DescribeResponse);
                case RtspMethod.SETUP:
                    return Task.FromResult(SetupInterleavedResponse);
                case RtspMethod.PLAY:
                    return Task.FromResult(PlayResponse);
                default:
                    throw new ArgumentException($"Method \"{requestMessage.Method}\" is not supported",
                        nameof(requestMessage));
            }
        }

        public Task SendRequestAsync(RtspRequestMessage requestMessage, CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }
}