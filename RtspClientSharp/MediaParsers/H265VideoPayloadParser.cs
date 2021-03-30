using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RtspClientSharp.MediaParsers
{
    class H265VideoPayloadParser : MediaPayloadParser
    {
        private readonly H265Parser _h265Parser;
        private readonly MemoryStream _nalStream;

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
