using System;

namespace RtspClientSharp.Rtcp
{
    interface IRtcpSenderStatisticsProvider
    {
        DateTime LastTimeReportReceived { get; }
        long LastNtpTimeReportReceived { get; }
    }
}