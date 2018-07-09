using System;
using System.Diagnostics;
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
        private readonly ElasticBuffer _nalBuffer;
        private bool _waitForStartFu = true;

        public H264VideoPayloadParser(H264CodecInfo codecInfo)
        {
            if (codecInfo == null)
                throw new ArgumentNullException(nameof(codecInfo));
            if (codecInfo.SpsPpsBytes == null)
                throw new ArgumentException($"{nameof(codecInfo.SpsPpsBytes)} is null", nameof(codecInfo));

            _h264Parser = new H264Parser {FrameGenerated = OnFrameGenerated};

            if (codecInfo.SpsPpsBytes.Length != 0)
                _h264Parser.Parse(new ArraySegment<byte>(codecInfo.SpsPpsBytes));

            _nalBuffer = new ElasticBuffer(8 * 1024, 512 * 1024);
        }

        public override void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit)
        {
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");

            PackModeType packMode = (PackModeType) (byteSegment.Array[byteSegment.Offset] & 0x1F);

            switch (packMode)
            {
                case PackModeType.FU_A:
                    ParseFU(timeOffset, byteSegment, 0, markerBit);
                    break;
                case PackModeType.FU_B:
                    ParseFU(timeOffset, byteSegment, DecodingOrderNumberFieldSize, markerBit);
                    break;
                case PackModeType.STAP_A:
                    ParseSTAP(timeOffset, byteSegment, 0, markerBit);
                    break;
                case PackModeType.STAP_B:
                    ParseSTAP(timeOffset, byteSegment, DecodingOrderNumberFieldSize, markerBit);
                    break;
                case PackModeType.MTAP16:
                    ParseMTAP(timeOffset, byteSegment, 2, markerBit);
                    break;
                case PackModeType.MTAP24:
                    ParseMTAP(timeOffset, byteSegment, 3, markerBit);
                    break;
                default:
                    _h264Parser.Parse(byteSegment);

                    if (markerBit)
                    {
                        DateTime timestamp = GetFrameTimestamp(timeOffset);
                        _h264Parser.GenerateFrame(timestamp);
                    }

                    break;
            }
        }

        public override void ResetState()
        {
            _nalBuffer.ResetState();
            _h264Parser.ResetState();
        }

        private void ParseFU(TimeSpan timeOffset, ArraySegment<byte> byteSegment, int donFieldSize, bool markerBit)
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
                    _nalBuffer.AddBytes(H264Parser.StartMarkerSegment);

                _nalBuffer.AddBytes(nalUnitSegment);

                if (endFlag)
                {
                    _h264Parser.Parse(nalUnitSegment, true);

                    if (markerBit)
                    {
                        DateTime timestamp = GetFrameTimestamp(timeOffset);
                        _h264Parser.GenerateFrame(timestamp);
                    }
                }
                else
                    _waitForStartFu = false;

                return;
            }

            if (_waitForStartFu)
                return;

            offset += donFieldSize + 1;

            _nalBuffer.AddBytes(new ArraySegment<byte>(byteSegment.Array, offset,
                byteSegment.Offset + byteSegment.Count - offset));

            if (endFlag)
            {
                ArraySegment<byte> nalUnitSegment = _nalBuffer.GetAccumulatedBytes();
                _h264Parser.Parse(nalUnitSegment, true);

                DateTime timestamp = GetFrameTimestamp(timeOffset);
                _h264Parser.GenerateFrame(timestamp);

                _waitForStartFu = true;
            }
        }

        private void ParseSTAP(TimeSpan timeOffset, ArraySegment<byte> byteSegment, int donFieldSize, bool markerBit)
        {
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");

            int startOffset = byteSegment.Offset + 1 + donFieldSize;
            int endOffset = byteSegment.Offset + byteSegment.Count;

            while (startOffset < endOffset)
            {
                int nalUnitSize = BigEndianConverter.ReadUInt16(byteSegment.Array, startOffset);

                startOffset += 2;

                var nalUnitSegment = new ArraySegment<byte>(byteSegment.Array, startOffset, nalUnitSize);
                _h264Parser.Parse(nalUnitSegment, true);

                startOffset += nalUnitSize;
            }

            if (!markerBit)
                return;

            DateTime timestamp = GetFrameTimestamp(timeOffset);
            _h264Parser.GenerateFrame(timestamp);
        }

        private void ParseMTAP(TimeSpan timeOffset, ArraySegment<byte> byteSegment, int tsOffsetFieldSize,
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
                _h264Parser.Parse(nalUnitSegment, true);

                startOffset += nalUnitSize;
            }

            if (!markerBit)
                return;

            DateTime timestamp = GetFrameTimestamp(timeOffset);
            _h264Parser.GenerateFrame(timestamp);
        }
    }
}