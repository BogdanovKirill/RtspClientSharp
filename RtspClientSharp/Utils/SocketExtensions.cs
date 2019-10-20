using System;
using System.Net;
using System.Net.Sockets;

namespace RtspClientSharp.Utils
{
    static class SocketExtensions
    {
        public static void JoinMulticastGroup(this Socket socket, IPAddress multicastGroupIp, IPAddress localIp)
        {
            MulticastOption multicastOption = new MulticastOption(multicastGroupIp, localIp);
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, multicastOption);
        }

        public static void LeaveMulticastGroup(this Socket socket, IPAddress multicastGroupIp)
        {
            MulticastOption multicastOption = new MulticastOption(multicastGroupIp);
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DropMembership, multicastOption);
        }

        public static void JoinMulticastSourceGroup(this Socket socket, IPAddress multicastGroupIp, IPAddress localIp, IPAddress sourceIp)
        {
            byte[] membershipAddresses = new byte[12]; // 3 IPs * 4 bytes (IPv4)
            Buffer.BlockCopy(multicastGroupIp.GetAddressBytes(), 0, membershipAddresses, 0, 4);
            Buffer.BlockCopy(sourceIp.GetAddressBytes(), 0, membershipAddresses, 4, 4);
            Buffer.BlockCopy(localIp.GetAddressBytes(), 0, membershipAddresses, 8, 4);
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddSourceMembership, membershipAddresses);
        }
    }
}