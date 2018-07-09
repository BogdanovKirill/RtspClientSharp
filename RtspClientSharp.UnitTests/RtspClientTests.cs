using System;
using System.Net;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using RtspClientSharp.Rtsp;
using RtspClientSharp.UnitTests.Rtsp;

namespace RtspClientSharp.UnitTests
{
    [TestClass]
    public class RtspClientTests
    {
        private static readonly TimeSpan SmallTimeoutInterval = TimeSpan.FromMilliseconds(100);

        private static readonly ConnectionParameters ConnectionParameters =
            new ConnectionParameters(new Uri("rtsp://127.0.0.1"))
            {
                ConnectTimeout = SmallTimeoutInterval,
                ReceiveTimeout = SmallTimeoutInterval
            };

        [TestMethod]
        [ExpectedException(typeof(InvalidCredentialException))]
        [DataRow(true, DisplayName = "RTSP over HTTP")]
        [DataRow(false, DisplayName = "RTSP over TCP")]
        public async Task ConnectAsync_Unauthorized_ThrowsInvalidCredentialException(bool emulateHttpException)
        {
            Exception exception;

            if (emulateHttpException)
                exception = new HttpBadResponseCodeException(HttpStatusCode.Unauthorized);
            else
                exception = new RtspBadResponseCodeException(RtspStatusCode.Unauthorized);

            var transportClientMock = new Mock<IRtspTransportClient>();
            transportClientMock.Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>()))
                .Throws(exception);

            var rtspClient = new RtspClient(ConnectionParameters, () => transportClientMock.Object);
            await rtspClient.ConnectAsync(CancellationToken.None);
        }

        [TestMethod]
        [ExpectedException(typeof(RtspClientException))]
        public async Task ConnectAsync_ConnectionTimeout_ThrowsException()
        {
            var transportClientMock = new Mock<IRtspTransportClient>();
            transportClientMock.Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.Delay(10));

            var rtspClient = new RtspClient(ConnectionParameters, () => transportClientMock.Object);
            await rtspClient.ConnectAsync(CancellationToken.None);
        }

        [TestMethod]
        [ExpectedException(typeof(OperationCanceledException), AllowDerivedTypes = true)]
        public async Task ConnectAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            var transportClientMock = new Mock<IRtspTransportClient>();
            transportClientMock.Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.Delay(10));
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            var rtspClient = new RtspClient(ConnectionParameters, () => transportClientMock.Object);
            await rtspClient.ConnectAsync(cancellationTokenSource.Token);
        }

        [TestMethod]
        public async Task ConnectAsync_TestTransportClientThatEmulatesRtspServer_ConnectionEstablished()
        {
            var transportClient = new RtspTransportClientEmulator();
            var rtspClient = new RtspClient(ConnectionParameters, () => transportClient);
            await rtspClient.ConnectAsync(CancellationToken.None);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public async Task ReceiveAsync_ClientIsNotConnected_ThrowsInvalidOperationException()
        {
            var transportClient = new RtspTransportClientEmulator();
            var rtspClient = new RtspClient(ConnectionParameters, () => transportClient);
            await rtspClient.ReceiveAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task ReceiveAsync_InterleavedModeAndOneRtcpByePacketInStream_SuccessfullyFinished()
        {
            var transportClient = new RtspTransportClientEmulator();
            var rtspClient = new RtspClient(ConnectionParameters, () => transportClient);

            await rtspClient.ConnectAsync(CancellationToken.None);
            await rtspClient.ReceiveAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task ReceiveAsync_CancellationRequested_ImmediateReturn()
        {
            var transportClient = new RtspTransportClientEmulator();
            var rtspClient = new RtspClient(ConnectionParameters, () => transportClient);
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            await rtspClient.ConnectAsync(CancellationToken.None);
            await rtspClient.ReceiveAsync(cancellationTokenSource.Token);
        }
    }
}