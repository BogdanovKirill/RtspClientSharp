using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace RtspClientSharp.Rtsp
{
    class RtspResponseMessage : RtspMessage
    {
        private static readonly ArraySegment<byte> EmptySegment = new ArraySegment<byte>(Array.Empty<byte>(), 0, 0);

        public RtspStatusCode StatusCode { get; }

        public ArraySegment<byte> ResponseBody { get; set; } = EmptySegment;

        public RtspResponseMessage(RtspStatusCode statusCode, Version protocolVersion, uint cSeq,
            NameValueCollection headers)
            : base(cSeq, protocolVersion, headers)
        {
            StatusCode = statusCode;
        }

        public static RtspResponseMessage Parse(ArraySegment<byte> byteSegment)
        {
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");

            var headersStream = new MemoryStream(byteSegment.Array, byteSegment.Offset, byteSegment.Count, false);
            var headersReader = new StreamReader(headersStream);

            string startLine = headersReader.ReadLine();

            if (startLine == null)
                throw new RtspParseResponseException("Empty response");

            string[] tokens = GetFirstLineTokens(startLine);

            string rtspVersion = tokens[0];

            Version protocolVersion = ParseProtocolVersion(rtspVersion);
            RtspStatusCode statusCode = ParseStatusCode(tokens[1]);

            NameValueCollection headers = HeadersParser.ParseHeaders(headersReader);

            uint cSeq = 0;
            string cseqValue = headers.Get("CSEQ");

            if (cseqValue != null)
                uint.TryParse(cseqValue, out cSeq);

            return new RtspResponseMessage(statusCode, protocolVersion, cSeq, headers);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("RTSP/{0} {1} {2}\r\nCSeq: {3}\r\n",
                ProtocolVersion, (int) StatusCode, StatusCode, CSeq);

            foreach (string key in Headers.AllKeys)
                sb.AppendFormat("{0}: {1}\r\n", key, Headers.Get(key));

            if (ResponseBody.Count != 0)
            {
                sb.AppendLine();

                string bodyString = Encoding.ASCII.GetString(ResponseBody.Array,
                    ResponseBody.Offset, ResponseBody.Count);

                sb.Append(bodyString);
            }

            return sb.ToString();
        }

        private static RtspStatusCode ParseStatusCode(string statusCode)
        {
            if (!int.TryParse(statusCode, out int code))
                throw new RtspParseResponseException($"Invalid status code: {statusCode}");

            return (RtspStatusCode) code;
        }

        private static string[] GetFirstLineTokens(string startLine)
        {
            string[] tokens = startLine.Split(' ');

            if (tokens.Length == 0)
                throw new RtspParseResponseException("Missing method");
            if (tokens.Length == 1)
                throw new RtspParseResponseException("Missing URI");
            if (tokens.Length == 2)
                throw new RtspParseResponseException("Missing protocol version");

            return tokens;
        }

        private static Version ParseProtocolVersion(string protocolNameVersion)
        {
            int slashPos = protocolNameVersion.IndexOf('/');

            if (slashPos == -1)
                throw new RtspParseResponseException($"Invalid protocol name/version format: {protocolNameVersion}");

            string version = protocolNameVersion.Substring(slashPos + 1);
            if (!Version.TryParse(version, out Version protocolVersion))
                throw new RtspParseResponseException($"Invalid RTSP protocol version: {version}");

            return protocolVersion;
        }
    }
}