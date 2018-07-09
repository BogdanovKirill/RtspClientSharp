using System.IO;

namespace RtspClientSharp.Rtcp
{
    abstract class RtcpSdesItem
    {
        public abstract int SerializedLength { get; }

        public abstract void Serialize(Stream stream);
    }
}