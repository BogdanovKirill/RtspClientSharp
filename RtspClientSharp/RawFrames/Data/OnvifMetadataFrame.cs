using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RtspClientSharp.RawFrames.Data
{
    public class OnvifMetadataFrame : RawFrame
    {
        public override FrameType Type => FrameType.Data;

        public string XmlSegment { get; private set; }


        public OnvifMetadataFrame(DateTime timestamp, string xmlFrameSegment)
            : base(timestamp, new ArraySegment<byte>(Encoding.UTF8.GetBytes(xmlFrameSegment)))
        {
            XmlSegment = xmlFrameSegment;
        }
    }
}