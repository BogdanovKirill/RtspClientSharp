using System.Net.Sockets;

namespace RtspClientSharp.Utils
{
    static class NetworkClientFactory
    {
        private const int TcpReceiveBufferDefaultSize = 64 * 1024;
        private const int SIO_UDP_CONNRESET = -1744830452;
        private static readonly byte[] EmptyOptionInValue = { 0, 0, 0, 0 };

        public static Socket CreateTcpClient()
        {
            return new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)
            {
                SendBufferSize = 0,
                ReceiveBufferSize = TcpReceiveBufferDefaultSize,
                DualMode = true,
                NoDelay = true
            };
        }

        public static Socket CreateUdpClient()
        {
            var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp)
            {
                SendBufferSize = 0,
                DualMode = true
            };
            socket.IOControl((IOControlCode)SIO_UDP_CONNRESET, EmptyOptionInValue, null);
            return socket;
        }
    }
}