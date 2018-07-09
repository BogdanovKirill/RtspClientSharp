using System;

namespace RtspClientSharp.Rtcp
{
    interface IRtcpSenderStatisticsProvider
    {
        DateTime LastTimeReportReceived { get; }
        ulong LastNtpTimeReportReceived { get; }
    }
}