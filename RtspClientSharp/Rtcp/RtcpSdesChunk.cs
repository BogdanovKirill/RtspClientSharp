using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RtspClientSharp.Rtcp
{
    class RtcpSdesChunk
    {
        public uint SyncSourceId { get; }
        public IReadOnlyList<RtcpSdesItem> Items { get; }

        public int SerializedLength
        {
            get { return 4 + Items.Sum(item => item.SerializedLength); }
        }

        public RtcpSdesChunk(uint syncSourceId, IReadOnlyList<RtcpSdesItem> items)
        {
            SyncSourceId = syncSourceId;
            Items = items;
        }

        public void Serialize(Stream stream)
        {
            stream.WriteByte((byte) (SyncSourceId >> 24));
            stream.WriteByte((byte) (SyncSourceId >> 16));
            stream.WriteByte((byte) (SyncSourceId >> 8));
            stream.WriteByte((byte) SyncSourceId);

            foreach (RtcpSdesItem item in Items)
                item.Serialize(stream);
        }
    }
}