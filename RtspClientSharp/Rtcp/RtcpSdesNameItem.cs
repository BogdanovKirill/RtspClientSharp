using System;
using System.IO;
using System.Text;

namespace RtspClientSharp.Rtcp
{
    class RtcpSdesNameItem : RtcpSdesItem
    {
        public string DomainName { get; }

        public RtcpSdesNameItem(string domainName)
        {
            DomainName = domainName ?? throw new ArgumentNullException(nameof(domainName));
        }

        public override int SerializedLength => 2 + GetDomainLength() + 1;

        public override void Serialize(Stream stream)
        {
            int domainByteLength = GetDomainLength();
            byte[] domainNameBytes = Encoding.ASCII.GetBytes(DomainName);

            stream.WriteByte(1);
            stream.WriteByte((byte) (domainByteLength + 1));
            stream.Write(domainNameBytes, 0, domainByteLength);
            stream.WriteByte(0);
        }

        private int GetDomainLength()
        {
            return Math.Min(Encoding.ASCII.GetByteCount(DomainName), 254);
        }
    }
}