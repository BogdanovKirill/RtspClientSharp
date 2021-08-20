using RtspClientSharp.Codecs.Video;
using System;
using System.IO;

namespace RtspClientSharp.MediaParsers
{
    class H265VideoPayloadParser : MediaPayloadParser
    {
        private readonly H265VideoHeaderParser _h265VideoHeaderParser;
        private readonly MemoryStream _nalStream;

        public H265VideoPayloadParser(H265CodecInfo codecInfo)
        {
            if (codecInfo == null)
                throw new ArgumentNullException(nameof(codecInfo));
            if (codecInfo.VpsBytes == null)
                throw new ArgumentNullException($"{nameof(codecInfo.VpsBytes)} is null", nameof(codecInfo));
            if (codecInfo.SpsBytes == null)
                throw new ArgumentNullException($"{nameof(codecInfo.SpsBytes)} is null", nameof(codecInfo));
            if (codecInfo.PpsBytes == null)
                throw new ArgumentNullException($"{nameof(codecInfo.PpsBytes)} is null", nameof(codecInfo));
           
            _h265VideoHeaderParser = new H265VideoHeaderParser { FrameGenerated = OnFrameGenerated };

            if (codecInfo.VpsBytes.Length != 0)
                _h265VideoHeaderParser.Parse(new ArraySegment<byte>(codecInfo.VpsBytes), false);
            if (codecInfo.SpsBytes.Length != 0)
                _h265VideoHeaderParser.Parse(new ArraySegment<byte>(codecInfo.SpsBytes), false);
            if (codecInfo.PpsBytes.Length != 0)
                _h265VideoHeaderParser.Parse(new ArraySegment<byte>(codecInfo.PpsBytes), false);

            _nalStream = new MemoryStream(4 * 1024);
        }

        public override void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit)
        {
            throw new NotImplementedException();
        }

        public override void ResetState()
        {
            throw new NotImplementedException();
        }
    }
}
