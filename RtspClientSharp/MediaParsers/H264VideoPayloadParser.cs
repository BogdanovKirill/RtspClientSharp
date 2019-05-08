using System;
using System.Diagnostics;
using System.IO;
using RtspClientSharp.Codecs.Video;
using RtspClientSharp.RawFrames.Video;
using RtspClientSharp.Utils;

namespace RtspClientSharp.MediaParsers
{
    class H264VideoPayloadParser : MediaPayloadParser
    {
        enum PackModeType
        {
            STAP_A = 24,
            STAP_B = 25,
            MTAP16 = 26,
            MTAP24 = 27,
            FU_A = 28,
            FU_B = 29
        }

        const int DecodingOrderNumberFieldSize = 2;
        const int DondFieldSize = 1;

        private readonly H264Parser _h264Parser;
        private readonly MemoryStream _nalStream;
        private bool _waitForStartFu = true;
        private TimeSpan _timeOffset = TimeSpan.MinValue;

        public H264VideoPayloadParser(H264CodecInfo codecInfo)
        {
            if (codecInfo == null)
                throw new ArgumentNullException(nameof(codecInfo));
            if (codecInfo.SpsPpsBytes == null)
                throw new ArgumentException($"{nameof(codecInfo.SpsPpsBytes)} is null", nameof(codecInfo));

            _h264Parser = new H264Parser(() => GetFrameTimestamp(_timeOffset)) {FrameGenerated = OnFrameGenerated};

            if (codecInfo.SpsPpsBytes.Length != 0)
                _h264Parser.Parse(new ArraySegment<byte>(codecInfo.SpsPpsBytes), false, false);

            _nalStream = new MemoryStream(8 * 1024);
        }

        public override void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit)
        {
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");

            if (!markerBit && timeOffset != _timeOffset)
                _h264Parser.TryGenerateFrame();
            
            _timeOffset = timeOffset;

            PackModeType packMode = (PackModeType) (byteSegment.Array[byteSegment.Offset] & 0x1F);
            
            switch (packMode)
            {
                case PackModeType.FU_A:
                    ParseFU(byteSegment, 0, markerBit);
                    break;
                case PackModeType.FU_B:
                    ParseFU(byteSegment, DecodingOrderNumberFieldSize, markerBit);
                    break;
                case PackModeType.STAP_A:
                    ParseSTAP(byteSegment, 0, markerBit);
                    break;
                case PackModeType.STAP_B:
                    ParseSTAP(byteSegment, DecodingOrderNumberFieldSize, markerBit);
                    break;
                case PackModeType.MTAP16:
                    ParseMTAP(byteSegment, 2, markerBit);
                    break;
                case PackModeType.MTAP24:
                    ParseMTAP(byteSegment, 3, markerBit);
                    break;
                default:
                    _h264Parser.Parse(byteSegment, false, markerBit);
                    break;
            }
        }

        public override void ResetState()
        {
            _nalStream.Position = 0;
            _h264Parser.ResetState();
            _waitForStartFu = true;
        }

        private void ParseFU(ArraySegment<byte> byteSegment, int donFieldSize, bool markerBit)
        {
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");

            int offset = byteSegment.Offset + 1;
            int fuHeader = byteSegment.Array[offset];
            bool startFlag = (fuHeader & 0x80) != 0;
            bool endFlag = (fuHeader & 0x40) != 0;

            if (startFlag)
            {
                int type = (fuHeader & 0x1F) | (byteSegment.Array[byteSegment.Offset] & 0xE0);

                offset += donFieldSize;
                byteSegment.Array[offset] = (byte) type;

                var nalUnitSegment = new ArraySegment<byte>(byteSegment.Array, offset,
                    byteSegment.Offset + byteSegment.Count - offset);

                if (!ArrayUtils.StartsWith(nalUnitSegment.Array, nalUnitSegment.Offset, nalUnitSegment.Count,
                    RawH264Frame.StartMarker))
                    _nalStream.Write(H264Parser.StartMarkerSegment.Array, H264Parser.StartMarkerSegment.Offset, H264Parser.StartMarkerSegment.Count);

                _nalStream.Write(nalUnitSegment.Array, nalUnitSegment.Offset, nalUnitSegment.Count);

                if (endFlag)
                {
                    _h264Parser.Parse(nalUnitSegment, true, markerBit);
                    _waitForStartFu = true;
                }
                else
                    _waitForStartFu = false;

                return;
            }

            if (_waitForStartFu)
                return;

            offset += donFieldSize + 1;

            _nalStream.Write(byteSegment.Array, offset, byteSegment.Offset + byteSegment.Count - offset);

            if (endFlag)
            {
                var nalUnitSegment = new ArraySegment<byte>(_nalStream.GetBuffer(), 0, (int)_nalStream.Position);
                _nalStream.Position = 0;
                _h264Parser.Parse(nalUnitSegment, true, markerBit);
                _waitForStartFu = true;
            }
        }

        private void ParseSTAP(ArraySegment<byte> byteSegment, int donFieldSize,
            bool markerBit)
        {
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");

            int startOffset = byteSegment.Offset + 1 + donFieldSize;
            int endOffset = byteSegment.Offset + byteSegment.Count;

            while (startOffset < endOffset)
            {
                int nalUnitSize = BigEndianConverter.ReadUInt16(byteSegment.Array, startOffset);

                startOffset += 2;

                var nalUnitSegment = new ArraySegment<byte>(byteSegment.Array, startOffset, nalUnitSize);

                startOffset += nalUnitSize;

                _h264Parser.Parse(nalUnitSegment, true, markerBit && startOffset >= endOffset);
            }
        }

        private void ParseMTAP(ArraySegment<byte> byteSegment, int tsOffsetFieldSize,
            bool markerBit)
        {
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");

            int startOffset = byteSegment.Offset;
            int endOffset = byteSegment.Offset + byteSegment.Count;

            startOffset += 1 + DecodingOrderNumberFieldSize;

            while (startOffset < endOffset)
            {
                int nalUnitSize = BigEndianConverter.ReadUInt16(byteSegment.Array, startOffset);

                startOffset += 2 + DondFieldSize + tsOffsetFieldSize;

                var nalUnitSegment = new ArraySegment<byte>(byteSegment.Array, startOffset, nalUnitSize);

                startOffset += nalUnitSize;

                _h264Parser.Parse(nalUnitSegment, true, markerBit && startOffset >= endOffset);
            }
        }
    }
}