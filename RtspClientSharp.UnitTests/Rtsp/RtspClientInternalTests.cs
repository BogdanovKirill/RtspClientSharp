using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Rtsp;

namespace RtspClientSharp.UnitTests.Rtsp
{
    [TestClass]
    public class RtspClientInternalTests
    {
        private readonly ConnectionParameters _fakeConnectionParameters =
            new ConnectionParameters(new Uri("rtsp://127.0.0.1"));

        [TestMethod]
        public async Task ConnectAsync_TestTransportClientThatEmulatesRtspServer_ConnectionEstablished()
        {
            var transportClient = new RtspTransportClientEmulator();

            var rtspClient = new RtspClientInternal(_fakeConnectionParameters, () => transportClient);
            await rtspClient.ConnectAsync(CancellationToken.None);
        }

        [TestMethod]
        [ExpectedException(typeof(OperationCanceledException))]
        public async Task ConnectAsync_CancellationRequested_ThrowsException()
        {
            var transportClient = new RtspTransportClientEmulator();
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            var rtspClient = new RtspClientInternal(_fakeConnectionParameters, () => transportClient);

            await rtspClient.ConnectAsync(cancellationTokenSource.Token);
        }

        [TestMethod]
        public async Task ReceiveAsync_InterleavedModeAndOneRtcpByePacketInStream_SuccessfullyFinished()
        {
            var transportClient = new RtspTransportClientEmulator();
            var rtspClient = new RtspClientInternal(_fakeConnectionParameters, () => transportClient);

            await rtspClient.ConnectAsync(CancellationToken.None);
            await rtspClient.ReceiveAsync(CancellationToken.None);
        }

        [TestMethod]
        public async Task ReceiveAsync_CancellationRequested_ImmediateReturn()
        {
            var transportClient = new RtspTransportClientEmulator();
            var rtspClient = new RtspClientInternal(_fakeConnectionParameters, () => transportClient);
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            await rtspClient.ConnectAsync(CancellationToken.None);
            await rtspClient.ReceiveAsync(cancellationTokenSource.Token);
        }
    }
}