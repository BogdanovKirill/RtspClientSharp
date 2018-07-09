using System;
using System.Collections.Generic;

namespace RtspClientSharp.Rtcp
{
    class RtcpStream : ITransportStream, IRtcpSenderStatisticsProvider
    {
        public DateTime LastTimeReportReceived { get; private set; } = DateTime.MinValue;
        public ulong LastNtpTimeReportReceived { get; private set; }

        public event EventHandler SessionShutdown;

        public void Process(ArraySegment<byte> payloadSegment)
        {
            IEnumerable<RtcpPacket> packets = RtcpPacket.Parse(payloadSegment);

            foreach (RtcpPacket packet in packets)
            {
                switch (packet)
                {
                    case RtcpSenderReportPacket senderReport:
                        LastNtpTimeReportReceived = senderReport.NtpTimestamp;
                        LastTimeReportReceived = DateTime.UtcNow;
                        break;
                    case RtcpByePacket _:
                        SessionShutdown?.Invoke(this, EventArgs.Empty);
                        break;
                }
            }
        }
    }
}