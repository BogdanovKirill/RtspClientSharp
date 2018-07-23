using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Rtsp;

namespace RtspClientSharp.UnitTests.Rtsp
{
    [TestClass]
    public class RtspTransportClientTests
    {
        private class RtspTransportClientFake : RtspTransportClient
        {
            private readonly Func<string, string> _responseProvider;
            private readonly MemoryStream _requestStream = new MemoryStream();
            private MemoryStream _responseStream;

            public override EndPoint RemoteEndPoint => new IPEndPoint(0, 0);

            public RtspTransportClientFake(ConnectionParameters connectionParameters,
                Func<string, string> responseProvider)
                : base(connectionParameters)
            {
                _responseProvider = responseProvider;
            }

            public override Task ConnectAsync(CancellationToken token)
            {
                return Task.CompletedTask;
            }

            public override Stream GetStream()
            {
                return null;
            }

            public override void Dispose()
            {
            }

            protected override Task WriteAsync(byte[] buffer, int offset, int count)
            {
                _responseStream = null;
                _requestStream.Write(buffer, offset, count);
                return Task.CompletedTask;
            }

            protected override Task<int> ReadAsync(byte[] buffer, int offset, int count)
            {
                EnsureResponseCreated();

                int read = count > 0 ? _responseStream.Read(buffer, offset, 1) : 0;

                return Task.FromResult(read);
            }

            protected override Task ReadExactAsync(byte[] buffer, int offset, int count)
            {
                EnsureResponseCreated();

                int read = _responseStream.Read(buffer, offset, count);

                return Task.FromResult(read);
            }

            private void EnsureResponseCreated()
            {
                if (_responseStream == null)
                {
                    string response = _responseProvider(Encoding.ASCII.GetString(_requestStream.ToArray()));
                    _responseStream = new MemoryStream(Encoding.ASCII.GetBytes(response));
                }
            }
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Anonymous access")]
        [DataRow(true, DisplayName = "Authorized access")]
        public async Task EnsureExecuteRequest_TestRequestAndFakeResponse_ReturnsRtspResponseMessage(
            bool checkAuthorized)
        {
            const string optionsResponse = "RTSP/1.0 200 OK\r\n" +
                                           "CSeq: 1\r\n" +
                                           "Public: DESCRIBE, SETUP, TEARDOWN, PLAY, PAUSE\r\n\r\n";

            const string unauthResponse = "RTSP/1.0 401 Unauthorized\r\n" +
                                          "WWW-AUTHENTICATE: Basic realm=\"testRealm\"\r\n" +
                                          "CSeq: 1\r\n\r\n";

            var uri = new Uri("rtsp://admin:admin@127.0.0.1");
            var conParams = new ConnectionParameters(uri);
            var factory = new RtspRequestMessageFactory(uri, null);
            var client = new RtspTransportClientFake(conParams, request =>
            {
                if (!checkAuthorized)
                    return optionsResponse;

                if (request.IndexOf("Authorization", StringComparison.InvariantCultureIgnoreCase) == -1)
                    return unauthResponse;

                return optionsResponse;
            });

            var optionsRequest = factory.CreateOptionsRequest();
            RtspResponseMessage responseMessage =
                await client.EnsureExecuteRequest(optionsRequest, CancellationToken.None);

            Assert.AreEqual(RtspStatusCode.Ok, responseMessage.StatusCode);
            Assert.IsNotNull(responseMessage.Headers.Get("PUBLIC"));
        }
    }
}