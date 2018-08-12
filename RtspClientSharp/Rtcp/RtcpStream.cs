using System;
using System.Collections.Generic;
using System.Threading;

namespace RtspClientSharp.Rtcp
{
    class RtcpStream : ITransportStream, IRtcpSenderStatisticsProvider
    {
        private long _lastNtpTimeReportReceived;
        private long _lastTimeReportReceivedTicks = DateTime.MinValue.Ticks;

        public DateTime LastTimeReportReceived => new DateTime(Interlocked.Read(ref _lastTimeReportReceivedTicks));
        public long LastNtpTimeReportReceived => Interlocked.Read(ref _lastNtpTimeReportReceived);

        public event EventHandler SessionShutdown;

        public void Process(ArraySegment<byte> payloadSegment)
        {
            IEnumerable<RtcpPacket> packets = RtcpPacket.Parse(payloadSegment);

            foreach (RtcpPacket packet in packets)
            {
                switch (packet)
                {
                    case RtcpSenderReportPacket senderReport:
                        Interlocked.Exchange(ref _lastNtpTimeReportReceived, senderReport.NtpTimestamp);
                        Interlocked.Exchange(ref _lastTimeReportReceivedTicks, DateTime.UtcNow.Ticks);
                        break;
                    case RtcpByePacket _:
                        SessionShutdown?.Invoke(this, EventArgs.Empty);
                        break;
                }
            }
        }
    }
}