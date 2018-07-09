using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using RtspClientSharp.Utils;

namespace RtspClientSharp.Rtsp
{
    class RtspTcpTransportClient : RtspTransportClient
    {
        private TcpClient _tcpClient;
        private Stream _networkStream;

        public RtspTcpTransportClient(ConnectionParameters connectionParameters)
            : base(connectionParameters)
        {
        }

        public override async Task ConnectAsync(CancellationToken token)
        {
            _tcpClient = TcpClientFactory.Create();

            Uri connectionUri = ConnectionParameters.ConnectionUri;

            int rtspPort = connectionUri.Port != -1 ? connectionUri.Port : Constants.DefaultRtspPort;

            await _tcpClient.ConnectAsync(connectionUri.Host, rtspPort);

            _networkStream = _tcpClient.GetStream();
        }

        public override Stream GetStream()
        {
            if (_tcpClient == null || !_tcpClient.Connected)
                throw new InvalidOperationException("Client is not connected");

            return _networkStream;
        }

        public override void Dispose()
        {
            if (_tcpClient == null)
                return;

            _tcpClient.Client.Close();
            _tcpClient.Dispose();
            _tcpClient = null;
        }

        protected override Task WriteAsync(byte[] buffer, int offset, int count)
        {
            Debug.Assert(_networkStream != null, "_networkStream != null");
            return _networkStream.WriteAsync(buffer, offset, count);
        }

        protected override Task<int> ReadAsync(byte[] buffer, int offset, int count)
        {
            Debug.Assert(_networkStream != null, "_networkStream != null");
            return _networkStream.ReadAsync(buffer, offset, count);
        }

        protected override Task ReadExactAsync(byte[] buffer, int offset, int count)
        {
            Debug.Assert(_networkStream != null, "_networkStream != null");
            return _networkStream.ReadExactAsync(buffer, offset, count);
        }
    }
}