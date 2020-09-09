using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace RtspClientSharp.Utils
{
    public interface ISocketFactory
    {
        IRtspSocket CreateTcpSocket();
        IRtspSocket CreateUdpSocket();
    }

    public interface IRtspSocket : IDisposable
    {
        void Connect(EndPoint endPoint);
        Task ConnectAsync(string host, int port);
        EndPoint RemoteEndPoint { get; }
        bool Connected { get; }
        void Close();
        Task SendAsync(ArraySegment<byte>[] data, SocketFlags socketFlags);
        Task SendAsync(ArraySegment<byte> data, SocketFlags socketFlags);
        Task<int> ReceiveAsync(ArraySegment<byte> data, SocketFlags socketFlags);
        void Bind(EndPoint endPoint);
        EndPoint LocalEndPoint { get; }
        NetworkStream CreateNetworkStream();
    }

    public class RtspSocketWrapper : IRtspSocket
    {
        private readonly Socket _socket;
        public RtspSocketWrapper(Socket socket)
        {
            _socket = socket;
        }

        public EndPoint RemoteEndPoint => _socket.RemoteEndPoint;

        public bool Connected => _socket.Connected;

        public EndPoint LocalEndPoint => _socket.LocalEndPoint;

        public void Bind(EndPoint endPoint)
        {
            _socket.Bind(endPoint);
        }

        public void Close()
        {
            _socket.Close();
        }

        public void Connect(EndPoint endPoint)
        {
            _socket.Connect(endPoint);
        }

        public Task ConnectAsync(string host, int port)
        {
            return _socket.ConnectAsync(host, port);
        }

        public NetworkStream CreateNetworkStream()
        {
            return new NetworkStream(_socket, false);
        }

        public void Dispose()
        {
            _socket.Dispose();
        }

        public Task<int> ReceiveAsync(ArraySegment<byte> data, SocketFlags socketFlags)
        {
            return _socket.ReceiveAsync(data, socketFlags);
        }

        public Task SendAsync(ArraySegment<byte>[] data, SocketFlags socketFlags)
        {
            return _socket.SendAsync(data, socketFlags);
        }

        public Task SendAsync(ArraySegment<byte> data, SocketFlags socketFlags)
        {
            return _socket.SendAsync(data, socketFlags);
        }
    }
}
