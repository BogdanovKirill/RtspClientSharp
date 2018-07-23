using RtspClientSharp.Utils;

namespace RtspClientSharp.Rtp
{
    internal interface IRtpSequenceAssembler
    {
        RefAction<RtpPacket> PacketPassed { get; set; }

        void ProcessPacket(ref RtpPacket rtpPacket);
    }
}