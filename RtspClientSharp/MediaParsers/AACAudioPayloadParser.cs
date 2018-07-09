using System;
using System.Diagnostics;
using RtspClientSharp.Codecs.Audio;
using RtspClientSharp.RawFrames.Audio;
using RtspClientSharp.Utils;

namespace RtspClientSharp.MediaParsers
{
    class AACAudioPayloadParser : MediaPayloadParser
    {
        private readonly AACCodecInfo _codecInfo;
        private readonly BitStreamReader _bitStreamReader = new BitStreamReader();

        public AACAudioPayloadParser(AACCodecInfo codecInfo)
        {
            _codecInfo = codecInfo ?? throw new ArgumentNullException(nameof(codecInfo));
        }

        public override void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit)
        {
            int auHeadersBitLength = BigEndianConverter.ReadUInt16(byteSegment.Array, byteSegment.Offset);
            int auHeadersLengthBytes = (auHeadersBitLength + 7) / 8;

            int headerBitSize = _codecInfo.SizeLength + _codecInfo.IndexLength;
            int audioBitsAvail = auHeadersBitLength - headerBitSize;

            if (audioBitsAvail < 0 || headerBitSize <= 0)
                return;

            int framesCount = 1 + audioBitsAvail / (_codecInfo.SizeLength + _codecInfo.IndexDeltaLength);

            _bitStreamReader.ReInitialize(byteSegment.SubSegment(2));

            int offset = byteSegment.Offset + auHeadersLengthBytes;

            for (int i = 0; i < framesCount; ++i)
            {
                int frameSize = _bitStreamReader.ReadBits(_codecInfo.SizeLength);

                if (i == 0)
                    _bitStreamReader.ReadBits(_codecInfo.IndexLength);
                else if (_codecInfo.IndexDeltaLength != 0)
                    _bitStreamReader.ReadBits(_codecInfo.IndexDeltaLength);

                Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");
                var frameBytes = new ArraySegment<byte>(byteSegment.Array, offset, frameSize);

                DateTime timestamp = GetFrameTimestamp(timeOffset);

                var aacFrame = new RawAACFrame(timestamp, frameBytes,
                    new ArraySegment<byte>(_codecInfo.ConfigBytes));

                OnFrameGenerated(aacFrame);
                offset += frameSize;
            }
        }

        public override void ResetState()
        {
        }
    }
}