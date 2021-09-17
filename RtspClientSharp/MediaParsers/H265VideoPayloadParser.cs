using Logger;
using RtspClientSharp.Codecs.Video;
using RtspClientSharp.RawFrames.Video;
using RtspClientSharp.Rtp;
using RtspClientSharp.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RtspClientSharp.MediaParsers
{
    class H265VideoPayloadParser : MediaPayloadParser
    {
        private readonly H265Parser _h265Parser;
        private readonly MemoryStream _nalStream;
        private bool _waitForStartFu = true;
        private bool _usingDonlField;
        private TimeSpan _timeOffset = TimeSpan.MinValue;

        int receivedFUs;


        public H265VideoPayloadParser(H265CodecInfo codecInfo)
        {
            ValidateCodecInfo(codecInfo);

            _h265Parser = new H265Parser { FrameGenerated = OnFrameGenerated };

            _usingDonlField = codecInfo.HasDonlField;
            _h265Parser.SetUsingDonlField(_usingDonlField);

            CheckBytesLength(codecInfo);

            _nalStream = new MemoryStream(8 * 1024);

            receivedFUs = 0;
        }

        public override void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit)
        {
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");

            //if(!markerBit && timeOffset != _timeOffset)

            _timeOffset = timeOffset;

            /*    
              0                   1                   2                   3
             0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |V=2|P|X|  CC   |M|     PT      |       sequence number         |
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |                           timestamp                           |
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |           synchronization source (SSRC) identifier            |
            +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
            |            contributing source (CSRC) identifiers             |
            |                             ....                              |
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+    

            */
            int nalUnit = (byteSegment.Array[byteSegment.Offset] >> 1) & 0x3F;

            if (!RtpH265TypeUtils.CheckIfIsValid(nalUnit))
                throw new H265ParserException($"Invalid Nal unit type { nalUnit }");

            RtpH265NALUType packMode = (RtpH265NALUType)nalUnit;

            PlayerLogger.fLogMethod($"Received NAL unit { packMode }\n");

            switch (packMode)
            {
                /*  supplemental enhancement information (SEI) */
                case RtpH265NALUType.PREFIX_SEI_NUT:
                    break;
                /* aggregated packet (AP) - with two or more NAL units */
                case RtpH265NALUType.RTPHEVC_AP:
                    DecodeAP(byteSegment, true);
                    break;
                /* fragmentation unit (FU) */
                case RtpH265NALUType.RTPHEVC_FP:
                    receivedFUs++;
                    PlayerLogger.fLogMethod($"Fragmentation Unit { receivedFUs } byteArray\n");
                    PlayerLogger.fLogMethod(PlayerLogger.LogArray(byteSegment.Array));
                    DecodeFP(byteSegment, true);
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

        private void DecodeAP(ArraySegment<byte> byteSegment, bool markerBit)
        {
            Debug.WriteLine("Aggregation packet");
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");

            int startOffset = byteSegment.Offset;
            int endOffset = byteSegment.Offset + byteSegment.Count;

            startOffset += 2;

            while (startOffset < (byteSegment.Count - 1))
            {

            }
        }

        private void DecodeFP(ArraySegment<byte> byteSegment, bool markerBit)
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
                throw new H264ParserException($"Illegal combination of S and E bit in RTP/HEVC packet");

            // Pass the HEVC FU header
            offset += RtpH265TypeUtils.RtpHevcFuHeaderSize;

            // Pass the HEVC DONL Field 
            if (_usingDonlField)
                offset += RtpH265TypeUtils.RtpHevcDonlFieldSize;

            if (startMarker)
            {
                // Start of Fragment.
                int[] newNalHeader = new int[2];

                // Reconstrut the NAL header from the rtp payload header, replacing the Type with FU Type           
                newNalHeader[0] = (byteSegment.Array[offset] & 0x81) | (fuType << 1);
                newNalHeader[1] = byteSegment.Array[offset + 1];

                offset += newNalHeader.Length;

                var nalUnitSegment = new ArraySegment<byte>(byteSegment.Array, offset, byteSegment.Offset + byteSegment.Count - offset);

                if (!ArrayUtils.StartsWith(nalUnitSegment.Array, nalUnitSegment.Offset, nalUnitSegment.Count,
                    RawH265Frame.StartMarker))
                    _nalStream.Write(H265Parser.StartMarkSegment.Array, H265Parser.StartMarkSegment.Offset, H265Parser.StartMarkSegment.Count);

                _nalStream.WriteByte((byte)newNalHeader[0]);
                _nalStream.WriteByte((byte)newNalHeader[1]);

                _nalStream.Write(nalUnitSegment.Array, nalUnitSegment.Offset, nalUnitSegment.Count);

                PlayerLogger.fLogMethod($"_nalStream after FU { receivedFUs } \n");
                PlayerLogger.fLogMethod(PlayerLogger.LogArray(_nalStream.ToArray()));

                _waitForStartFu = false;

                return;
            }

            if (_waitForStartFu)
                return;

            _nalStream.Write(byteSegment.Array, byteSegment.Offset, byteSegment.Offset + byteSegment.Count - offset);
            PlayerLogger.fLogMethod($"_nalStream after FU { receivedFUs } ");
            PlayerLogger.fLogMethod(PlayerLogger.LogArray(_nalStream.ToArray()));

            if (endMarker)
            {
                // End part of Fragment
                PlayerLogger.fLogMethod($"_nalStream after endMarker FU { receivedFUs } ");
                PlayerLogger.fLogMethod(PlayerLogger.LogArray(_nalStream.ToArray()));

                var nalUnitSegment = new ArraySegment<byte>(_nalStream.GetBuffer(), 0, (int)_nalStream.Position);

                PlayerLogger.fLogMethod("reconstructed NAL unit (extracted from _nalStream)");
                PlayerLogger.fLogMethod(PlayerLogger.LogArray(_nalStream.ToArray()));

                _nalStream.Position = 0;
                _h265Parser.Parse(nalUnitSegment, markerBit);
                _waitForStartFu = true;
            }
        }
    }
}
