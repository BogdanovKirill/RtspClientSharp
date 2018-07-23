using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace RtspClientSharp.Rtsp
{
    internal interface IRtspTransportClient : IDisposable
    {
        EndPoint RemoteEndPoint { get; }

        Task ConnectAsync(CancellationToken token);

        Stream GetStream();

        Task<RtspResponseMessage> EnsureExecuteRequest(RtspRequestMessage requestMessage, CancellationToken token,
            int responseReadPortionSize = 0);

        Task<RtspResponseMessage> ExecuteRequest(RtspRequestMessage requestMessage, CancellationToken token,
            int responseReadPortionSize = 0);

        Task SendRequestAsync(RtspRequestMessage requestMessage, CancellationToken token);
    }
}