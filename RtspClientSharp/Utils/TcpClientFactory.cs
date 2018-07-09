using System.Net.Sockets;

namespace RtspClientSharp.Utils
{
    static class TcpClientFactory
    {
        public static TcpClient Create()
        {
            return new TcpClient(AddressFamily.InterNetworkV6)
            {
                SendBufferSize = 0,
                Client = {DualMode = true},
                NoDelay = true
            };
        }
    }
}