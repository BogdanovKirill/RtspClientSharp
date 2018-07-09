using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using RtspClientSharp.Codecs;
using RtspClientSharp.Codecs.Audio;
using RtspClientSharp.Codecs.Video;
using RtspClientSharp.RawFrames.Video;
using RtspClientSharp.Utils;

namespace RtspClientSharp.Sdp
{
    class SdpParser
    {
        private class PayloadFormatInfo
        {
            public string TrackName { get; set; }
            public CodecInfo CodecInfo { get; set; }
            public int SamplesFrequency { get; set; }

            public PayloadFormatInfo(CodecInfo codecInfo, int samplesFrequency)
            {
                CodecInfo = codecInfo;
                SamplesFrequency = samplesFrequency;
            }
        }

        private readonly Dictionary<int, PayloadFormatInfo> _payloadFormatNumberToInfoMap =
            new Dictionary<int, PayloadFormatInfo>();

        private PayloadFormatInfo _lastParsedFormatInfo;

        public IEnumerable<RtspTrackInfo> Parse(ArraySegment<byte> payloadSegment)
        {
            Debug.Assert(payloadSegment.Array != null, "payloadSegment.Array != null");

            if (payloadSegment.Count == 0)
                throw new ArgumentException("Empty SDP document", nameof(payloadSegment));

            _payloadFormatNumberToInfoMap.Clear();
            _lastParsedFormatInfo = null;

            var sdpStream = new MemoryStream(payloadSegment.Array, payloadSegment.Offset, payloadSegment.Count);
            var sdpStreamReader = new StreamReader(sdpStream);

            string line;
            while (!string.IsNullOrEmpty(line = sdpStreamReader.ReadLine()))
            {
                if (line[0] == 'm')
                    ParseMediaLine(line);
                else if (line[0] == 'a')
                    ParseAttributesLine(line);
            }

            return _payloadFormatNumberToInfoMap.Values
                .Where(fi => fi.TrackName != null && fi.CodecInfo != null)
                .Select(fi => new RtspMediaTrackInfo(fi.TrackName, fi.CodecInfo, fi.SamplesFrequency));
        }

        private void ParseMediaLine(string line)
        {
            int lastSpaceIndex = line.LastIndexOf(' ');

            if (lastSpaceIndex == -1)
                return;

            string payloadFormat = line.Substring(lastSpaceIndex);

            if (!int.TryParse(payloadFormat, out int payloadFormatNumber))
                return;

            CodecInfo codecInfo = TryCreateCodecInfo(payloadFormatNumber);
            int samplesFrequency = GetSamplesFrequencyFromPayloadType(payloadFormatNumber);

            _lastParsedFormatInfo = new PayloadFormatInfo(codecInfo, samplesFrequency);
            _payloadFormatNumberToInfoMap[payloadFormatNumber] = _lastParsedFormatInfo;
        }

        private void ParseAttributesLine(string line)
        {
            int equalsSignIndex = line.IndexOf('=');

            if (equalsSignIndex == -1)
                return;

            int colonIndex = line.IndexOf(':', equalsSignIndex);

            if (colonIndex == -1)
                return;

            ++equalsSignIndex;

            int attributeLength = colonIndex - equalsSignIndex;

            if (attributeLength == 0)
                return;

            string attributeName = line.Substring(equalsSignIndex, attributeLength).Trim().ToUpperInvariant();

            ++colonIndex;

            if (colonIndex == line.Length)
                return;

            string attributeValue = line.Substring(colonIndex).TrimStart();

            switch (attributeName)
            {
                case "RTPMAP":
                    ParseRtpMapAttribute(attributeValue);
                    break;
                case "CONTROL":
                    ParseControlAttribute(attributeValue);
                    break;
                case "FMTP":
                    ParseFmtpAttribute(attributeValue);
                    break;
            }
        }

        private void ParseRtpMapAttribute(string attributeValue)
        {
            int spaceIndex = attributeValue.IndexOf(' ');

            if (spaceIndex < 1)
                return;

            string payloadFormat = attributeValue.Substring(0, spaceIndex);

            if (!int.TryParse(payloadFormat, out int rtpPayloadFormatNumber))
                return;

            int nonSpaceIndex = spaceIndex;

            while (attributeValue[nonSpaceIndex] == ' ')
                if (++nonSpaceIndex == attributeValue.Length)
                    return;

            string codecName;
            int samplesFrequency = 0;
            int channels = 0;

            int slashIndex = attributeValue.IndexOf('/', nonSpaceIndex);

            if (slashIndex == -1)
                codecName = attributeValue.Substring(nonSpaceIndex);
            else
            {
                codecName = attributeValue.Substring(nonSpaceIndex, slashIndex - nonSpaceIndex);

                ++slashIndex;

                int nextSlashIndex = attributeValue.IndexOf('/', slashIndex);

                if (nextSlashIndex == -1)
                    int.TryParse(attributeValue.Substring(slashIndex), out samplesFrequency);
                else
                {
                    int.TryParse(attributeValue.Substring(slashIndex, nextSlashIndex - slashIndex),
                        out samplesFrequency);

                    int.TryParse(attributeValue.Substring(++nextSlashIndex), out channels);
                }
            }

            if (!_payloadFormatNumberToInfoMap.TryGetValue(rtpPayloadFormatNumber,
                out PayloadFormatInfo payloadFormatInfo))
                return;

            if (samplesFrequency == 0)
                samplesFrequency = payloadFormatInfo.SamplesFrequency;
            else
                payloadFormatInfo.SamplesFrequency = samplesFrequency; //override default

            string codecNameUpperCase = codecName.ToUpperInvariant();
            payloadFormatInfo.CodecInfo = TryCreateCodecInfo(codecNameUpperCase, samplesFrequency, channels);
        }

        private void ParseControlAttribute(string attributeValue)
        {
            if (_lastParsedFormatInfo != null)
                _lastParsedFormatInfo.TrackName = attributeValue;
        }

        private void ParseFmtpAttribute(string attributeValue)
        {
            int spaceIndex = attributeValue.IndexOf(' ');

            if (spaceIndex < 1)
                return;

            string payloadFormat = attributeValue.Substring(0, spaceIndex);

            if (!int.TryParse(payloadFormat, out int rtpPayloadFormatNumber))
                return;

            if (!_payloadFormatNumberToInfoMap.TryGetValue(rtpPayloadFormatNumber,
                out PayloadFormatInfo payloadFormatInfo))
                return;

            int nonSpaceIndex = spaceIndex;

            while (attributeValue[nonSpaceIndex] == ' ')
                if (++nonSpaceIndex == attributeValue.Length)
                    return;

            string formatAttributesString = attributeValue.Substring(nonSpaceIndex);
            string[] formatAttributes = Array.ConvertAll(formatAttributesString.Split(';'), p => p.Trim());

            if (payloadFormatInfo.CodecInfo is H264CodecInfo h264CodecInfo)
                ParseH264FormatAttributes(formatAttributes, h264CodecInfo);
            else if (payloadFormatInfo.CodecInfo is AACCodecInfo aacCodecInfo)
                ParseAACFormatAttributes(formatAttributes, aacCodecInfo);
        }

        private static void ParseH264FormatAttributes(string[] formatAttributes, H264CodecInfo h264CodecInfo)
        {
            string spropParametersSet = formatAttributes.FirstOrDefault(fa =>
                fa.StartsWith("sprop-parameter-sets", StringComparison.InvariantCultureIgnoreCase));

            if (spropParametersSet == null)
                return;

            string spropParametersSetValue = GetFormatParameterValue(spropParametersSet);

            int commaIndex = spropParametersSetValue.IndexOf(',');

            if (commaIndex == -1)
            {
                byte[] sps = RawH264Frame.StartMarker.Concat(
                    Convert.FromBase64String(spropParametersSetValue)).ToArray();

                h264CodecInfo.SpsPpsBytes = sps;
            }
            else
            {
                IEnumerable<byte> sps = RawH264Frame.StartMarker.Concat(
                    Convert.FromBase64String(spropParametersSetValue.Substring(0, commaIndex)));

                ++commaIndex;

                IEnumerable<byte> pps = RawH264Frame.StartMarker.Concat(
                    Convert.FromBase64String(spropParametersSetValue.Substring(commaIndex)));

                h264CodecInfo.SpsPpsBytes = sps.Concat(pps).ToArray();
            }
        }

        private static void ParseAACFormatAttributes(string[] formatAttributes, AACCodecInfo aacCodecInfo)
        {
            string sizeLengthParameter = formatAttributes.FirstOrDefault(fa =>
                fa.StartsWith("sizeLength", StringComparison.InvariantCultureIgnoreCase));

            if (sizeLengthParameter == null)
                throw new SdpParserException("SizeLength parameters is not found");

            string indexLengthParameter = formatAttributes.FirstOrDefault(fa =>
                fa.StartsWith("indexLength", StringComparison.InvariantCultureIgnoreCase));

            if (indexLengthParameter == null)
                throw new SdpParserException("IndexLength parameters is not found");

            string indexDeltaLengthParameter = formatAttributes.FirstOrDefault(fa =>
                fa.StartsWith("indexDeltaLength", StringComparison.InvariantCultureIgnoreCase));

            if (indexDeltaLengthParameter == null)
                throw new SdpParserException("IndexDeltaLength parameters is not found");

            aacCodecInfo.SizeLength = int.Parse(GetFormatParameterValue(sizeLengthParameter));
            aacCodecInfo.IndexLength = int.Parse(GetFormatParameterValue(indexLengthParameter));
            aacCodecInfo.IndexDeltaLength = int.Parse(GetFormatParameterValue(indexDeltaLengthParameter));

            string configParameter = formatAttributes.FirstOrDefault(fa =>
                fa.StartsWith("config", StringComparison.InvariantCultureIgnoreCase));

            if (configParameter != null)
                aacCodecInfo.ConfigBytes = Hex.StringToByteArray(GetFormatParameterValue(configParameter));
        }

        private static string GetFormatParameterValue(string formatParameter)
        {
            if (formatParameter == null) throw new ArgumentNullException(nameof(formatParameter));

            int equalsSignIndex = formatParameter.IndexOf('=');

            if (equalsSignIndex == -1)
                throw new SdpParserException($"Bad parameter format: {formatParameter}");

            ++equalsSignIndex;

            if (equalsSignIndex == formatParameter.Length)
                throw new SdpParserException($"Empty parameter value: {formatParameter}");

            return formatParameter.Substring(equalsSignIndex);
        }

        private static CodecInfo TryCreateCodecInfo(int payloadFormatNumber)
        {
            CodecInfo codecInfo = null;

            switch (payloadFormatNumber)
            {
                case 0:
                    codecInfo = new G711UCodecInfo();
                    break;
                case 2:
                    codecInfo = new G726CodecInfo(32 * 1000);
                    break;
                case 8:
                    codecInfo = new G711ACodecInfo();
                    break;
                case 10:
                    codecInfo = new PCMCodecInfo(44100, 16, 2);
                    break;
                case 11:
                    codecInfo = new PCMCodecInfo(44100, 16, 1);
                    break;
                case 26:
                    codecInfo = new MJPEGCodecInfo();
                    break;
                case 105:
                    codecInfo = new H264CodecInfo();
                    break;
            }

            return codecInfo;
        }

        private static CodecInfo TryCreateCodecInfo(string codecName, int samplesFrequency, int channels)
        {
            if (codecName == "JPEG")
                return new MJPEGCodecInfo();

            if (codecName == "H264")
                return new H264CodecInfo();

            bool isPcmu = codecName == "PCMU";
            bool isPcma = codecName == "PCMA";

            if (isPcmu || isPcma)
            {
                G711CodecInfo g711CodecInfo;

                if (isPcmu)
                    g711CodecInfo = new G711UCodecInfo();
                else
                    g711CodecInfo = new G711ACodecInfo();

                if (samplesFrequency != 0)
                    g711CodecInfo.SampleRate = samplesFrequency;
                if (channels != 0)
                    g711CodecInfo.Channels = channels;

                return g711CodecInfo;
            }

            if (codecName == "L16" || codecName == "L8")
                return new PCMCodecInfo(samplesFrequency != 0 ? samplesFrequency : 8000,
                    codecName == "L16" ? 16 : 8, channels != 0 ? channels : 1);

            if (codecName == "MPEG4-GENERIC")
                return new AACCodecInfo();

            if (codecName.Contains("726"))
            {
                int bitrate;

                if (codecName.Contains("16"))
                    bitrate = 16000;
                else if (codecName.Contains("24"))
                    bitrate = 24000;
                else if (codecName.Contains("32"))
                    bitrate = 32000;
                else if (codecName.Contains("40"))
                    bitrate = 40000;
                else
                    return null;

                var g726CodecInfo = new G726CodecInfo(bitrate);

                if (samplesFrequency != 0)
                    g726CodecInfo.SampleRate = samplesFrequency;
                else if (channels != 0)
                    g726CodecInfo.Channels = channels;

                return g726CodecInfo;
            }

            return null;
        }

        protected static int GetSamplesFrequencyFromPayloadType(int payloadFormatNumber)
        {
            switch (payloadFormatNumber)
            {
                case 0:
                case 2:
                case 3:
                case 4:
                case 5:
                case 7:
                case 8:
                case 9:
                case 12:
                case 15:
                case 18:
                    return 8000;
                case 6:
                    return 16000;
                case 10:
                case 11:
                    return 44100;
                case 16:
                    return 11025;
                case 17:
                    return 22050;
                case 14:
                case 25:
                case 26:
                case 28:
                case 31:
                case 32:
                case 33:
                case 34:
                    return 90000;
            }

            return 0;
        }
    }
}