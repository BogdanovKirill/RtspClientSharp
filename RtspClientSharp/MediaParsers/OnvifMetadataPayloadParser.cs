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
            _tag += xml;

            if (_tag.StartsWith("<?xml"))
            {
                int ind = _tag.IndexOf("?>");
                if (ind != -1)
                    _tag = _tag.Remove(0, ind + 2);
                else
                    return;
            }

            if (_tagSeparator == null)
            {
                var rg = Regex.Match(_tag, "<(.*?)>");
                if (rg.Success && rg.Groups.Count > 1)
                {
                    _tagSeparator = $"</{rg.Groups[1].Value.Split(' ').FirstOrDefault()}>";
                }
                else
                {
                    return;
                }
            }

            do
            {
                var endIndex = _tag.IndexOf(_tagSeparator);

                if (endIndex != -1)
                {
                    string xmlFrame = _tag.Substring(0, endIndex + _tagSeparator.Length);
                    _tag = _tag.Remove(0, xmlFrame.Length);
                    OnFrameGenerated(new OnvifMetadataFrame(DateTime.Now, xmlFrame.Trim('\n', '\r')));
                }
                else
                {
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
