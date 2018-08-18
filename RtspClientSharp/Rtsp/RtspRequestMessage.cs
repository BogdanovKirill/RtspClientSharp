using System;
using System.Text;

namespace RtspClientSharp.Rtsp
{
    class RtspRequestMessage : RtspMessage
    {
        private readonly Func<uint> _cSeqProvider;

        public RtspMethod Method { get; }
        public Uri ConnectionUri { get; }
        public string UserAgent { get; }

        public RtspRequestMessage(RtspMethod method, Uri connectionUri, Version protocolVersion, Func<uint> cSeqProvider,
            string userAgent, string session)
            : base(cSeqProvider(), protocolVersion)
        {
            Method = method;
            ConnectionUri = connectionUri;
            _cSeqProvider = cSeqProvider;
            UserAgent = userAgent;

            if (!string.IsNullOrEmpty(session))
                Headers.Add("Session", session);
        }
        
        public void UpdateSequenceNumber()
        {
            CSeq = _cSeqProvider();
        }

        public override string ToString()
        {
            var queryBuilder = new StringBuilder(512);

            queryBuilder.AppendFormat("{0} {1} RTSP/{2}\r\n", Method, ConnectionUri, ProtocolVersion.ToString(2));
            queryBuilder.AppendFormat("CSeq: {0}\r\n", CSeq);

            if (!string.IsNullOrEmpty(UserAgent))
                queryBuilder.AppendFormat("User-Agent: {0}\r\n", UserAgent);

            foreach (string headerName in Headers.AllKeys)
                queryBuilder.AppendFormat("{0}: {1}\r\n", headerName, Headers[headerName]);

            queryBuilder.Append("\r\n");

            return queryBuilder.ToString();
        }
    }
}