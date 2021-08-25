using RtspClientSharp.Codecs.Video;
using RtspClientSharp.RawFrames.Video;
using RtspClientSharp.Rtp;
using RtspClientSharp.Utils;
using System;
using System.Diagnostics;
using System.IO;

namespace RtspClientSharp.MediaParsers
{
    class H265VideoPayloadParser : MediaPayloadParser
    {
        private readonly H265Parser _h265Parser;
        private readonly MemoryStream _nalStream;
        private bool _waitForStartFu = true;
        private TimeSpan _timeOffset = TimeSpan.MinValue;

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

            _h265Parser = new H265Parser { FrameGenerated = OnFrameGenerated };

            if (codecInfo.VpsBytes.Length != 0)
                _h265Parser.Parse(new ArraySegment<byte>(codecInfo.VpsBytes), false);
            if (codecInfo.SpsBytes.Length != 0)
                _h265Parser.Parse(new ArraySegment<byte>(codecInfo.SpsBytes), false);
            if (codecInfo.PpsBytes.Length != 0)
                _h265Parser.Parse(new ArraySegment<byte>(codecInfo.PpsBytes), false);

            _nalStream = new MemoryStream(8 * 1024);
        }

        public override void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit)
        {
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");

            //if(!markerBit && timeOffset != _timeOffset)

            _timeOffset = timeOffset;

            int nalUnit = (byteSegment.Array[byteSegment.Offset] >> 1) & 0x3F;

            if (!RtpH265TypeUtils.CheckIfIsValid(nalUnit))
                throw new H265ParserException($"Invalid Nal unit type { nalUnit }");

            RtpH265NALUType packMode = (RtpH265NALUType)nalUnit;

            switch (packMode)
            {
                /*  supplemental enhancement information (SEI) */
                case RtpH265NALUType.PREFIX_SEI_NUT:
                    break;
                /* aggregated packet (AP) - with two or more NAL units */
                case RtpH265NALUType.RTPHEVC_AP:
                    break;
                /* fragmentation unit (FU) */
                case RtpH265NALUType.RTPHEVC_FP:
                    DecodeFP(byteSegment, RtpH265TypeUtils.RtpHevcDonlFieldSize, true);
                    break;
                default:
                    _h265Parser.Parse(byteSegment, markerBit);
                    break;
            }
        }

        public override void ResetState()
        {
            _nalStream.Position = 0;
            _h265Parser.ResetState();
            _waitForStartFu = true;
        }

        private void DecodeFP(ArraySegment<byte> byteSegment, int donFieldSize, bool markerBit)
        {
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");

            /*
            *    decode the FU header
            *
            *     0 1 2 3 4 5 6 7
            *    +-+-+-+-+-+-+-+-+
            *    |S|E|  FuType   |
            *    +---------------+
            *
            *       Start fragment (S): 1 bit
            *       End fragment (E): 1 bit
            *       FuType: 6 bits
            */

            /* pass the HEVC payload header */
            int offset = byteSegment.Offset + RtpH265TypeUtils.RtpHevcPayloadHeaderSize;
            int fuHeader = byteSegment.Array[offset];
            bool firstFragment = (fuHeader & 0x80) != 0;
            bool lastFragment = (fuHeader & 0x40) != 0;

            if (firstFragment && lastFragment)
                throw new H264ParserException($"Illegal combination of S and E bit in RTP/HEVC packet");

            if (firstFragment)
            {
                int fuType = byteSegment.Array[offset] & 0x3f;
                
                offset += donFieldSize;
                byteSegment.Array[offset] = (byte)fuType;

                var nalUnitSegment = new ArraySegment<byte>(byteSegment.Array, offset,
                    byteSegment.Offset + byteSegment.Count - offset);

                if (!ArrayUtils.StartsWith(nalUnitSegment.Array, nalUnitSegment.Offset, nalUnitSegment.Count,
                    RawH265Frame.StartMarker))
                    _nalStream.Write(H265Parser.StartMarkSegment.Array, H265Parser.StartMarkSegment.Offset, H265Parser.StartMarkSegment.Count);

                _nalStream.Write(nalUnitSegment.Array, nalUnitSegment.Offset, nalUnitSegment.Count);

                if (lastFragment)
                {
                    _h265Parser.Parse(nalUnitSegment, markerBit);
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

            if(lastFragment)
            {
                var nalUnitSegment = new ArraySegment<byte>(_nalStream.GetBuffer(), 0, (int)_nalStream.Position);
                _nalStream.Position = 0;
                _h265Parser.Parse(nalUnitSegment, markerBit);
                _waitForStartFu = true;
            }
        }
    }
}
