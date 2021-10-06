using Logger;
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
        private bool _usingDonlField;
        private TimeSpan _timeOffset = TimeSpan.MinValue;

        //private int _receivedFuCount;

        public H265VideoPayloadParser(H265CodecInfo codecInfo)
        {
            ValidateCodecInfo(codecInfo);

            _h265Parser = new H265Parser(() => GetFrameTimestamp(_timeOffset)) { FrameGenerated = OnFrameGenerated };

            _usingDonlField = codecInfo.HasDonlField;

            CheckBytesLength(codecInfo);

            _nalStream = new MemoryStream(8 * 1024);

            //_receivedFuCount = 0;
        }

        public override void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit)
        {
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");

            if (!markerBit && timeOffset != _timeOffset)
                _h265Parser.TryGetFrameBytes();

            _timeOffset = timeOffset;

            int nalUnit = (byteSegment.Array[byteSegment.Offset] >> 1) & 0x3F;

            if (!RtpH265TypeUtils.CheckIfIsValid(nalUnit))
                throw new H265ParserException($"Invalid Nal unit type { nalUnit }");

            RtpH265NALUType nalUnitType = (RtpH265NALUType)nalUnit;

            switch (nalUnitType)
            {
                /* aggregated packet (AP) - with two or more NAL units */
                case RtpH265NALUType.RTPHEVC_AP:
                    ParseAP(byteSegment, markerBit);
                    break;
                /* fragmentation unit (FU) */
                case RtpH265NALUType.RTPHEVC_FP:
                    //_receivedFuCount++;
                    ParseFP(byteSegment, markerBit);
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

        private void ValidateCodecInfo(H265CodecInfo codecInfo)
        {
            if (codecInfo == null)
                throw new ArgumentNullException(nameof(codecInfo));
            if (codecInfo.VpsBytes == null)
                throw new ArgumentNullException($"{nameof(codecInfo.VpsBytes)} is null", nameof(codecInfo));
            if (codecInfo.SpsBytes == null)
                throw new ArgumentNullException($"{nameof(codecInfo.SpsBytes)} is null", nameof(codecInfo));
            if (codecInfo.PpsBytes == null)
                throw new ArgumentNullException($"{nameof(codecInfo.PpsBytes)} is null", nameof(codecInfo));
        }

        private void CheckBytesLength(H265CodecInfo codecInfo)
        {
            if (codecInfo.VpsBytes.Length != 0)
                _h265Parser.Parse(new ArraySegment<byte>(codecInfo.VpsBytes), false);

            if (codecInfo.SpsBytes.Length != 0)
                _h265Parser.Parse(new ArraySegment<byte>(codecInfo.SpsBytes), false);

            if (codecInfo.PpsBytes.Length != 0)
                _h265Parser.Parse(new ArraySegment<byte>(codecInfo.PpsBytes), false);
        }

        private void ParseAP(ArraySegment<byte> byteSegment, bool markerBit)
        {
            Debug.WriteLine("Aggregation packet");
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");

            /* pass the HEVC payload header */
            int offset = byteSegment.Offset + RtpH265TypeUtils.RtpHevcPayloadHeaderSize;

            /* pass the HEVC DONL field */
            if (_usingDonlField)
                offset += RtpH265TypeUtils.RtpHevcDonlFieldSize;

            while (offset < (byteSegment.Count - 1))
            {

            }
        }

        private void ParseFP(ArraySegment<byte> byteSegment, bool markerBit)
        {
            Debug.WriteLine("Fragmentation Unit");
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
            int fuType = fuHeader & 0x3f;
            bool startMarker = (fuHeader & 0x80) != 0;
            bool endMarker = (fuHeader & 0x40) != 0;

            // Start bit and End bit must not both be set to 1 in the same FU header
            if (startMarker && endMarker)
                throw new H265ParserException($"Illegal combination of S and E bit in RTP/HEVC packet");

            // Pass the HEVC FU header
            offset += RtpH265TypeUtils.RtpHevcFuHeaderSize;

            // Pass the HEVC DONL Field 
            if (_usingDonlField)
                offset += RtpH265TypeUtils.RtpHevcDonlFieldSize;
            
            if (startMarker)
            {
                // Start of Fragment.
                byte[] newNalHeader = new byte[2];

                // Reconstrut the NAL header from the rtp payload header, replacing the Type with FU Type           
                newNalHeader[0] = Convert.ToByte((byteSegment.Array[byteSegment.Offset] & 0x81) | (fuType << 1));
                newNalHeader[1] = byteSegment.Array[byteSegment.Offset + 1];

                offset += newNalHeader.Length;

                var nalUnitSegment = new ArraySegment<byte>(byteSegment.Array, offset, byteSegment.Offset + byteSegment.Count - offset);

                if (!ArrayUtils.StartsWith(nalUnitSegment.Array, nalUnitSegment.Offset, nalUnitSegment.Count,
                    RawH265Frame.StartMarker))
                    _nalStream.Write(H265Parser.StartMarkSegment.Array, H265Parser.StartMarkSegment.Offset, H265Parser.StartMarkSegment.Count);

                _nalStream.WriteByte(newNalHeader[0]);
                _nalStream.WriteByte(newNalHeader[1]);

                _nalStream.Write(nalUnitSegment.Array, nalUnitSegment.Offset, nalUnitSegment.Count);

                _waitForStartFu = false;

                return;
            }

            if (_waitForStartFu)
                return;

            _nalStream.Write(byteSegment.Array, offset, byteSegment.Offset + byteSegment.Count - offset);

            if (endMarker)
            {
                // End part of Fragment               
                var nalUnitSegment = new ArraySegment<byte>(_nalStream.GetBuffer(), 0, (int)_nalStream.Position);
                _nalStream.Position = 0;
                _h265Parser.Parse(nalUnitSegment, markerBit);
                _waitForStartFu = true;
            }
        }
    }
}
