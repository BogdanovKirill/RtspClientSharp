using System;
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
            var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)
            {
                ReceiveBufferSize = TcpReceiveBufferDefaultSize,
                DualMode = true,
                NoDelay = true
            };

            if (Environment.OSVersion.Platform != PlatformID.MacOSX)
                socket.SendBufferSize = 0;

            return socket;
        }

        public static Socket CreateUdpClient()
        {
            var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp)
            {
                DualMode = true
            };
            socket.IOControl((IOControlCode)SIO_UDP_CONNRESET, EmptyOptionInValue, null);

            if (Environment.OSVersion.Platform != PlatformID.MacOSX)
                socket.SendBufferSize = 0;

            return socket;
        }
    }
}