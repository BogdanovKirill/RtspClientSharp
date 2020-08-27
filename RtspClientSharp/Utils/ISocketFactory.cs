using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace RtspClientSharp.Utils
{
    public interface ISocketFactory
    {
        Socket CreateSocket();
    }
}
