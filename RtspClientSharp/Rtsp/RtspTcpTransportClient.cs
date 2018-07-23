using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using RtspClientSharp.Utils;

namespace RtspClientSharp.Rtsp
{
    class RtspTcpTransportClient : RtspTransportClient
    {
        private Socket _tcpClient;
        private Stream _networkStream;
        private EndPoint _remoteEndPoint = new IPEndPoint(IPAddress.None, 0);
        private int _disposed;

        public override EndPoint RemoteEndPoint => _remoteEndPoint;

        public RtspTcpTransportClient(ConnectionParameters connectionParameters)
            : base(connectionParameters)
        {
        }

        public override async Task ConnectAsync(CancellationToken token)
        {
            _tcpClient = NetworkClientFactory.CreateTcpClient();

            Uri connectionUri = ConnectionParameters.ConnectionUri;

            int rtspPort = connectionUri.Port != -1 ? connectionUri.Port : Constants.DefaultRtspPort;

            await _tcpClient.ConnectAsync(connectionUri.Host, rtspPort);

            _remoteEndPoint = _tcpClient.RemoteEndPoint;
            _networkStream = new NetworkStream(_tcpClient, false);
        }

        public override Stream GetStream()
        {
            if (_tcpClient == null || !_tcpClient.Connected)
                throw new InvalidOperationException("Client is not connected");

            return _networkStream;
        }

        public override void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            _tcpClient?.Close();
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