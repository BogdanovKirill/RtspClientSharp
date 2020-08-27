using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace RtspClientSharp.Utils
{
    public class DefaultTcpSocketFactory : ISocketFactory
    {
        private static ISocketFactory _instance;
        public static ISocketFactory Instance => _instance = (_instance ?? new DefaultTcpSocketFactory());
        private DefaultTcpSocketFactory()
        {
        }
        private const int TcpReceiveBufferDefaultSize = 64 * 1024;
        public Socket CreateSocket()
        {
            return new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp)
            {
                ReceiveBufferSize = TcpReceiveBufferDefaultSize,
                DualMode = true,
                NoDelay = true
            };
        }
    }
}
