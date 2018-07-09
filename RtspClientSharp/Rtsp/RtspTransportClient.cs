using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RtspClientSharp.Rtsp.Authentication;
using RtspClientSharp.Utils;

namespace RtspClientSharp.Rtsp
{
    abstract class RtspTransportClient : IRtspTransportClient
    {
        protected readonly ConnectionParameters ConnectionParameters;
        private readonly byte[] _readBuffer = new byte[Constants.MaxResponseHeadersSize];

        private Authenticator _authenticator;

        protected RtspTransportClient(ConnectionParameters connectionParameters)
        {
            ConnectionParameters =
                connectionParameters ?? throw new ArgumentNullException(nameof(connectionParameters));
        }

        public abstract Task ConnectAsync(CancellationToken token);

        public abstract Stream GetStream();

        public async Task<RtspResponseMessage> EnsureExecuteRequest(RtspRequestMessage requestMessage,
            CancellationToken token, int responseReadPortionSize = 0)
        {
            RtspResponseMessage responseMessage = await ExecuteRequest(requestMessage, token, responseReadPortionSize);

            if (responseMessage.StatusCode != RtspStatusCode.Ok)
                throw new RtspBadResponseCodeException(responseMessage.StatusCode);

            return responseMessage;
        }

        public async Task<RtspResponseMessage> ExecuteRequest(RtspRequestMessage requestMessage,
            CancellationToken token, int responseReadPortionSize = 0)
        {
            token.ThrowIfCancellationRequested();

            await SendRequestAsync(requestMessage);

            RtspResponseMessage responseMessage = await GetResponseAsync(responseReadPortionSize);

            if (responseMessage.StatusCode != RtspStatusCode.Unauthorized)
                return responseMessage;

            if (ConnectionParameters.Credentials.IsEmpty() || _authenticator != null)
                throw new RtspBadResponseCodeException(responseMessage.StatusCode);

            string authenticateHeader = responseMessage.Headers.Get(WellKnownHeaders.WwwAuthenticate);

            if (string.IsNullOrEmpty(authenticateHeader))
                throw new RtspBadResponseCodeException(responseMessage.StatusCode);

            _authenticator = Authenticator.Create(ConnectionParameters.Credentials, authenticateHeader);
            requestMessage.CSeq++;

            await SendRequestAsync(requestMessage);
            responseMessage = await GetResponseAsync();

            if (responseMessage.StatusCode == RtspStatusCode.Unauthorized)
                throw new RtspBadResponseCodeException(responseMessage.StatusCode);

            return responseMessage;
        }

        public Task SendRequestAsync(RtspRequestMessage requestMessage)
        {
            if (_authenticator != null)
                AddAuthorizationHeader(requestMessage);

            byte[] requestBytes = Encoding.ASCII.GetBytes(requestMessage.ToString());

            return WriteAsync(requestBytes, 0, requestBytes.Length);
        }

        public abstract void Dispose();

        protected abstract Task WriteAsync(byte[] buffer, int offset, int count);
        protected abstract Task<int> ReadAsync(byte[] buffer, int offset, int count);
        protected abstract Task ReadExactAsync(byte[] buffer, int offset, int count);

        private async Task<RtspResponseMessage> GetResponseAsync(int responseReadPortionSize = 0)
        {
            int totalRead = await ReadUntilEndOfHeadersAsync(responseReadPortionSize);

            int startOfResponse = ArrayUtils.IndexOfBytes(_readBuffer, Constants.RtspProtocolNameBytes, 0, totalRead);

            if (startOfResponse == -1)
                throw new RtspBadResponseException("\"RTSP\" start signature is not found");

            int endOfResponseHeaders = ArrayUtils.IndexOfBytes(_readBuffer, Constants.DoubleCrlfBytes,
                                           startOfResponse, totalRead - startOfResponse) +
                                       Constants.DoubleCrlfBytes.Length;

            if (endOfResponseHeaders == -1)
                throw new RtspBadResponseException("End of response headers is not found");

            var headersByteSegment =
                new ArraySegment<byte>(_readBuffer, startOfResponse, endOfResponseHeaders - startOfResponse);
            RtspResponseMessage rtspResponseMessage = RtspResponseMessage.Parse(headersByteSegment);

            string contentLengthString = rtspResponseMessage.Headers.Get(WellKnownHeaders.ContentLength);

            if (string.IsNullOrEmpty(contentLengthString))
                return rtspResponseMessage;

            if (!uint.TryParse(contentLengthString, out uint contentLength))
                throw new RtspParseResponseException($"Invalid content-length header: {contentLengthString}");

            if (contentLength == 0)
                return rtspResponseMessage;

            if (contentLength > Constants.MaxResponseHeadersSize)
                throw new RtspBadResponseException($"Response content is too large: {contentLength}");

            int dataPartSize = totalRead - headersByteSegment.Count;

            Buffer.BlockCopy(_readBuffer, endOfResponseHeaders, _readBuffer, 0, dataPartSize);
            await ReadExactAsync(_readBuffer, dataPartSize, (int) (contentLength - dataPartSize));

            rtspResponseMessage.ResponseBody = new ArraySegment<byte>(_readBuffer, 0, (int) contentLength);
            return rtspResponseMessage;
        }

        private async Task<int> ReadUntilEndOfHeadersAsync(int readPortionSize = 0)
        {
            int offset = 0;

            int endOfHeaders;
            int totalRead = 0;

            do
            {
                int count = _readBuffer.Length - totalRead;

                if (readPortionSize != 0 && count > readPortionSize)
                    count = readPortionSize;

                if (count == 0)
                    throw new RtspBadResponseException(
                        $"Response is too large (> {Constants.MaxResponseHeadersSize / 1024} KB)");

                int read = await ReadAsync(_readBuffer, offset, count);

                if (read == 0)
                    throw new EndOfStreamException("End of rtsp stream");

                offset += read;
                totalRead += read;

                endOfHeaders = ArrayUtils.IndexOfBytes(_readBuffer, Constants.DoubleCrlfBytes, 0, totalRead);
            } while (endOfHeaders == -1);

            return totalRead;
        }

        private void AddAuthorizationHeader(RtspRequestMessage request)
        {
            Uri uri = ConnectionParameters.GetFixedRtspUri();

            string headerValue = _authenticator.GetResponse(request.CSeq, uri.ToString(),
                request.Method.ToString(), Array.Empty<byte>());

            request.Headers.Add("Authorization", headerValue);
        }
    }
}