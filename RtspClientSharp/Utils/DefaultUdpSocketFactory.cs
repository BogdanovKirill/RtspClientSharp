using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace RtspClientSharp.Utils
{
    public class DefaultUdpSocketFactory : ISocketFactory
    {
        private const int UdpReceiveBufferDefaultSize = 128 * 1024;
        private const int SIO_UDP_CONNRESET = -1744830452;
        private static readonly byte[] EmptyOptionInValue = { 0, 0, 0, 0 };
        public Socket CreateSocket()
        {
            var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp)
            {
                ReceiveBufferSize = UdpReceiveBufferDefaultSize,
                DualMode = true
            };
            socket.IOControl((IOControlCode)SIO_UDP_CONNRESET, EmptyOptionInValue, null);
            return socket;
        }
    }
}
