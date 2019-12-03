using RtspClientSharp.Codecs.Data;
using RtspClientSharp.RawFrames.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace RtspClientSharp.MediaParsers
{
    class OnvifMetadataPayloadParser : MediaPayloadParser
    {
        private readonly OnvifMetadataCodecInfo _codecInfo;
        private string _tagSeparator;
        private string _tag;

        public OnvifMetadataPayloadParser(OnvifMetadataCodecInfo codecInfo)
        {
            _codecInfo = codecInfo ?? throw new ArgumentNullException(nameof(codecInfo));
        }

        public override void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit)
        {
            var xml = Encoding.Default.GetString(byteSegment.Array, byteSegment.Offset, byteSegment.Count);

            if (xml.StartsWith("<?xml"))
            {
                int ind = xml.IndexOf("?>");
                xml = xml.Remove(0, ind + 2);
            }

            if (_tagSeparator == null)
            {
                var rg = Regex.Match(xml, "<(.*?)>");
                if (rg.Success && rg.Groups.Count > 1)
                {
                    _tagSeparator = rg.Groups[1].Value.Split(' ').FirstOrDefault();
                }
            }

            var endTag = $"</{_tagSeparator}>";

            do
            {
                var endIndex = xml.IndexOf(endTag);

                if (endIndex != -1)
                {
                    string endOfNode = xml.Substring(0, endIndex + endTag.Length);
                
                    _tag += endOfNode;
                    _tag = _tag.Trim('\n', '\r');

                    OnFrameGenerated(new OnvifMetadataFrame(DateTime.Now, _tag));
                    _tag = "";

                    xml = xml.Replace(endOfNode, "");
                }
                else
                {
                    _tag += xml;
                    break;
                }
            }
            while (true);
        }

        public override void ResetState()
        {
            _tagSeparator = _tag = null;
        }
    }
}
