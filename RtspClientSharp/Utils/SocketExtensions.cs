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
            if (multicastGroupIp.AddressFamily == AddressFamily.InterNetwork)
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, multicastOption);
            else if (multicastGroupIp.AddressFamily == AddressFamily.InterNetworkV6)
                socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, multicastOption);
        }

        public static void LeaveMulticastGroup(this Socket socket, IPAddress multicastGroupIp)
        {
            MulticastOption multicastOption = new MulticastOption(multicastGroupIp);
            if (multicastGroupIp.AddressFamily == AddressFamily.InterNetwork)
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DropMembership, multicastOption);
            else if (multicastGroupIp.AddressFamily == AddressFamily.InterNetworkV6)
                socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.DropMembership, multicastOption);
        }

        public static void JoinMulticastSourceGroup(this Socket socket, IPAddress multicastGroupIp, IPAddress localIp, IPAddress sourceIp)
        {
            // let's try to convert all IPs to type provided by server in "destination" parameter
            if (multicastGroupIp.AddressFamily == AddressFamily.InterNetwork)
            {
                if (localIp.AddressFamily != AddressFamily.InterNetwork)
                {
                    if (localIp.IsIPv4MappedToIPv6)
                        localIp = localIp.MapToIPv4();
                    else
                        localIp = IPAddress.Any;
                }
                if (sourceIp.AddressFamily != AddressFamily.InterNetwork)
                {
                    if (sourceIp.IsIPv4MappedToIPv6)
                        sourceIp = sourceIp.MapToIPv4();
                    else
                        sourceIp = IPAddress.None;
                }
                if (!IPAddress.None.Equals(sourceIp))
                {
                    byte[] membershipAddresses = new byte[12]; // 3 IPs * 4 bytes (IPv4)
                    Buffer.BlockCopy(multicastGroupIp.GetAddressBytes(), 0, membershipAddresses, 0, 4);
                    Buffer.BlockCopy(sourceIp.GetAddressBytes(), 0, membershipAddresses, 4, 4);
                    Buffer.BlockCopy(localIp.GetAddressBytes(), 0, membershipAddresses, 8, 4);
                    socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddSourceMembership, membershipAddresses);
                }
                else
                {
                    // if we don't have good source IP, join group without source
                    JoinMulticastGroup(socket, multicastGroupIp, localIp);
                }
            }
            else if (multicastGroupIp.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (localIp.AddressFamily != AddressFamily.InterNetworkV6)
                {
                    localIp = localIp.MapToIPv6();
                }
                if (sourceIp.AddressFamily != AddressFamily.InterNetworkV6)
                {
                    sourceIp = sourceIp.MapToIPv6();
                }
                if (!IPAddress.None.Equals(sourceIp))
                {
                    byte[] membershipAddresses = new byte[48]; // 3 IPs * 16 bytes (IPv6)
                    Buffer.BlockCopy(multicastGroupIp.GetAddressBytes(), 0, membershipAddresses, 0, 16);
                    Buffer.BlockCopy(sourceIp.GetAddressBytes(), 0, membershipAddresses, 16, 16);
                    Buffer.BlockCopy(localIp.GetAddressBytes(), 0, membershipAddresses, 32, 16);
                    socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddSourceMembership, membershipAddresses);
                }
                else
                {
                    // if we don't have good source IP, join group without source
                    JoinMulticastGroup(socket, multicastGroupIp, localIp);
                }
            }
        }
    }
}