using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace RtspClientSharp.Utils
{
    public class DefaultSocketFactory : ISocketFactory
    {
        private static ISocketFactory _instance;
        public static ISocketFactory Instance => _instance = (_instance ?? new DefaultSocketFactory());
        private DefaultSocketFactory()
        {
        }

        private const int UdpReceiveBufferDefaultSize = 128 * 1024;
        private const int SIO_UDP_CONNRESET = -1744830452;
        private static readonly byte[] EmptyOptionInValue = { 0, 0, 0, 0 };
        public IRtspSocket CreateUdpSocket()
        {
            var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp)
            {
                ReceiveBufferSize = UdpReceiveBufferDefaultSize,
                DualMode = true
            };
            socket.IOControl((IOControlCode)SIO_UDP_CONNRESET, EmptyOptionInValue, null);
            return new RtspSocketWrapper(socket);
        }

        private const int TcpReceiveBufferDefaultSize = 64 * 1024;
        public IRtspSocket CreateTcpSocket()
        {
            return new RtspSocketWrapper(new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)
            {
                ReceiveBufferSize = TcpReceiveBufferDefaultSize,
                DualMode = true,
                NoDelay = true
            });
        }
    }
}
