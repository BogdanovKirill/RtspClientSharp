using System;
using System.Collections.Generic;
using System.Threading;

namespace RtspClientSharp.Rtsp
{
    public class RtspRequestParams
    {
        public DateTime? InitialTimestamp { get; set; }
        private bool _isSetTimestampInClock;
        public bool IsSetTimestampInClock
        {
            get
            {
                return _isSetTimestampInClock && InitialTimestamp != null;
            }
            set
            {
                _isSetTimestampInClock = value;
            }
        }
        public CancellationToken Token { get; set; }
        public Dictionary<string, string> Headers { get; set; }
    }
}
