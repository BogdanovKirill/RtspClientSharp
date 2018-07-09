using System.IO;

namespace RtspClientSharp.Rtcp
{
    interface ISerializablePacket
    {
        void Serialize(Stream stream);
    }
}