using System;
using System.Collections.Generic;
using System.Threading;

namespace RtspClientSharp.Rtsp
{
    public class RtspRequestParams
    {
        public DateTime InitialTimestamp { get; set; }
        public CancellationToken Token { get; set; }
        public Dictionary<string, string> Headers { get; set; }
    }
}
