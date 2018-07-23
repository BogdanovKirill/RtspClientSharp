using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RtspClientSharp.Rtsp.Authentication;
using RtspClientSharp.Utils;

namespace RtspClientSharp.Rtsp
{
    class RtspHttpTransportClient : RtspTransportClient
    {
        private Socket _streamDataClient;
        private Socket _commandsClient;
        private string _sessionCookie;
        private Authenticator _authenticator;
        private Stream _dataNetworkStream;
        private uint _commandCounter;
        private EndPoint _remoteEndPoint = new IPEndPoint(IPAddress.None, 0);
        private int _disposed;

        public override EndPoint RemoteEndPoint => _remoteEndPoint;

        public RtspHttpTransportClient(ConnectionParameters connectionParameters)
            : base(connectionParameters)
        {
        }

        public override async Task ConnectAsync(CancellationToken token)
        {
            _commandCounter = 0;
            _sessionCookie = Guid.NewGuid().ToString("N").Substring(0, 10);

            _streamDataClient = NetworkClientFactory.CreateTcpClient();

            Uri connectionUri = ConnectionParameters.ConnectionUri;

            int httpPort = connectionUri.Port != -1 ? connectionUri.Port : Constants.DefaultHttpPort;

            await _streamDataClient.ConnectAsync(connectionUri.Host, httpPort);

            _remoteEndPoint = _streamDataClient.RemoteEndPoint;
            _dataNetworkStream = new NetworkStream(_streamDataClient, false);

            string request = ComposeGetRequest();
            byte[] requestBytes = Encoding.ASCII.GetBytes(request);

            await _dataNetworkStream.WriteAsync(requestBytes, 0, requestBytes.Length, token);

            var buffer = new byte[Constants.MaxResponseHeadersSize];
            int read = await ReadUntilEndOfHeadersAsync(_dataNetworkStream, buffer);

            var ms = new MemoryStream(buffer, 0, read);
            var streamReader = new StreamReader(ms, Encoding.ASCII);

            string responseLine = streamReader.ReadLine();

            if (responseLine == null)
                throw new HttpBadResponseException("Empty response");

            string[] tokens = responseLine.Split(' ');

            if (tokens.Length != 3)
                throw new HttpRequestException("Invalid first response line");

            HttpStatusCode statusCode = (HttpStatusCode) int.Parse(tokens[1]);

            if (statusCode == HttpStatusCode.OK)
                return;

            if (statusCode == HttpStatusCode.Unauthorized &&
                !ConnectionParameters.Credentials.IsEmpty() &&
                _authenticator == null)
            {
                NameValueCollection headers = HeadersParser.ParseHeaders(streamReader);

                string authenticateHeader = headers.Get(WellKnownHeaders.WwwAuthenticate);

                if (string.IsNullOrEmpty(authenticateHeader))
                    throw new HttpBadResponseCodeException(statusCode);

                _authenticator = Authenticator.Create(ConnectionParameters.Credentials, authenticateHeader);

                _streamDataClient.Dispose();

                await ConnectAsync(token);
                return;
            }

            throw new HttpBadResponseCodeException(statusCode);
        }

        public override Stream GetStream()
        {
            if (_streamDataClient == null || !_streamDataClient.Connected)
                throw new InvalidOperationException("Client is not connected");

            return _dataNetworkStream;
        }

        public override void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            _streamDataClient?.Close();
            _commandsClient?.Close();
        }

        protected override async Task WriteAsync(byte[] buffer, int offset, int count)
        {
            using (_commandsClient = NetworkClientFactory.CreateTcpClient())
            {
                Uri connectionUri = ConnectionParameters.ConnectionUri;

                int httpPort = connectionUri.Port != -1 ? connectionUri.Port : Constants.DefaultHttpPort;

                await _commandsClient.ConnectAsync(connectionUri.Host, httpPort);

                string base64CodedCommandString = Convert.ToBase64String(buffer, offset, count);
                byte[] base64CommandBytes = Encoding.ASCII.GetBytes(base64CodedCommandString);

                string request = ComposePostRequest(base64CommandBytes);
                byte[] requestBytes = Encoding.ASCII.GetBytes(request);

                ArraySegment<byte>[] sendList =
                {
                    new ArraySegment<byte>(requestBytes),
                    new ArraySegment<byte>(base64CommandBytes)
                };

                await _commandsClient.SendAsync(sendList, SocketFlags.None);
            }
        }

        protected override Task<int> ReadAsync(byte[] buffer, int offset, int count)
        {
            Debug.Assert(_dataNetworkStream != null, "_dataNetworkStream != null");
            return _dataNetworkStream.ReadAsync(buffer, offset, count);
        }

        protected override Task ReadExactAsync(byte[] buffer, int offset, int count)
        {
            Debug.Assert(_dataNetworkStream != null, "_dataNetworkStream != null");
            return _dataNetworkStream.ReadExactAsync(buffer, offset, count);
        }

        private string ComposeGetRequest()
        {
            string authorizationHeader = GetAuthorizationHeader(++_commandCounter, "GET", Array.Empty<byte>());

            return $"GET {ConnectionParameters.ConnectionUri.PathAndQuery} HTTP/1.0\r\n" +
                   $"x-sessioncookie: {_sessionCookie}\r\n" +
                   $"{authorizationHeader}\r\n";
        }

        private string ComposePostRequest(byte[] commandBytes)
        {
            string authorizationHeader = GetAuthorizationHeader(++_commandCounter, "POST", commandBytes);

            return $"POST {ConnectionParameters.ConnectionUri.PathAndQuery} HTTP/1.0\r\n" +
                   $"x-sessioncookie: {_sessionCookie}\r\n" +
                   "Content-Type: application/x-rtsp-tunnelled\r\n" +
                   $"Content-Length: {commandBytes.Length}\r\n" +
                   $"{authorizationHeader}\r\n";
        }

        private string GetAuthorizationHeader(uint counter, string method, byte[] requestBytes)
        {
            string authorizationHeader;

            if (_authenticator != null)
            {
                string headerValue = _authenticator.GetResponse(counter,
                    ConnectionParameters.ConnectionUri.PathAndQuery,
                    method, requestBytes);

                authorizationHeader = $"Authorization: {headerValue}\r\n";
            }
            else
                authorizationHeader = string.Empty;

            return authorizationHeader;
        }

        private async Task<int> ReadUntilEndOfHeadersAsync(Stream stream, byte[] buffer)
        {
            int offset = 0;

            int endOfHeaders;
            int totalRead = 0;

            do
            {
                int count = buffer.Length - totalRead;

                if (count == 0)
                    throw new HttpBadResponseException($"Response is too large (> {buffer.Length / 1024} KB)");

                int read = await stream.ReadAsync(buffer, offset, count);

                if (read == 0)
                    throw new EndOfStreamException("End of http stream");

                totalRead += read;

                int startIndex = offset - (Constants.DoubleCrlfBytes.Length - 1);

                if (startIndex < 0)
                    startIndex = 0;

                endOfHeaders = ArrayUtils.LastIndexOfBytes(buffer, Constants.DoubleCrlfBytes, startIndex,
                    totalRead - startIndex);

                offset += read;
            } while (endOfHeaders == -1);

            return totalRead;
        }
    }
}