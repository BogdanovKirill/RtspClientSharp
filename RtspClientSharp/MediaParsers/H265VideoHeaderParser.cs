using RtspClientSharp.RawFrames;
using RtspClientSharp.RawFrames.Video;
using RtspClientSharp.Rtp;
using RtspClientSharp.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace RtspClientSharp.MediaParsers
{
    class H265VideoHeaderParser
    {
        public static readonly ArraySegment<byte> StartMarkSegment = new ArraySegment<byte>(RawH265Frame.StartMarker);

        private readonly Func<DateTime> _frameTimestampProvider;
        private readonly BitStreamReader _bitStreamReader = new BitStreamReader();
        private readonly Dictionary<int, byte[]> _vpsMap = new Dictionary<int, byte[]>();
        private readonly Dictionary<int, byte[]> _spsMap = new Dictionary<int, byte[]>();
        private readonly Dictionary<int, byte[]> _ppsMap = new Dictionary<int, byte[]>();
        private bool _updatedBytes;

        public Action<RawFrame> FrameGenerated;

        public void Parse(ArraySegment<byte> byteSegment, bool generateFrame)
        {
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");

            if (ArrayUtils.StartsWith(byteSegment.Array, byteSegment.Offset, byteSegment.Count,
                RawH265Frame.StartMarker))
                H265Slicer.Slice(byteSegment, SliceOnNalUnitFound);

        }

        private void SliceOnNalUnitFound(ArraySegment<byte> byteSegment)
        {
            bool generateFrame = false;
            ProcessNalUnit(byteSegment, true, ref generateFrame);
        }

        private void ProcessNalUnit(ArraySegment<byte> byteSegment, bool hasStartMarker, ref bool generateFrame)
        {
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");

            int offset = byteSegment.Offset;

            if (hasStartMarker)
                offset += RawH265Frame.StartMarkerSize;

            /*
         *  +---------------+---------------+
            |0|1|2|3|4|5|6|7|0|1|2|3|4|5|6|7|
            +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
            |F|   Type    |  LayerId  | TID |
            +-------------+-----------------+

            Forbidden zero (F): 1 bit
            NAL unit type (Type): 6 bits
            NUH layer ID (LayerId): 6 bits
            NUH temporal ID plus 1 (TID): 3 bits
        */

            // forbidden_zero_bit must be 0
            if (!(byteSegment.Array[offset] >> 0x0F == 0))
                return;

            int nalUnitType = (byteSegment.Array[offset] >> 1) & 0x3F;
            int layerId = ((byteSegment.Array[offset] << 5) & 0x20) | ((byteSegment.Array[offset + 1] >> 3) & 0x1F);
            int temporalId = byteSegment.Array[offset + 1] & 0x07;

            //Required to be equal to zero
            if (layerId != 0)
                throw new H265ParserException($"Invalid LayerId { layerId }");

            //The value of TemporalId is equal to TID minus 1. A TID value of 0 is illegal...
            if (temporalId == 0)
                throw new H265ParserException($"Invalid TemporalId (TID) { temporalId }");

            //Checking Nal unit type
            if (!RtpH265TypeUtils.CheckIfIsValid(nalUnitType))
                throw new H265ParserException($"Invalid Nal Unit Type { nalUnitType }");

            if ((RtpH265NALUType)nalUnitType == RtpH265NALUType.VPS_NUT)
            {
                ParseVps(byteSegment, hasStartMarker);
                return;
            }

            if ((RtpH265NALUType)nalUnitType == RtpH265NALUType.SPS_NUT)
            {
                ParseSps(byteSegment, hasStartMarker);
                return;
            }

            if ((RtpH265NALUType)nalUnitType == RtpH265NALUType.PPS_NUT)
            {
                ParsePps(byteSegment, hasStartMarker);
                return;
            }
        }

        private void ParseVps(ArraySegment<byte> byteSegment, bool hasStartMarker)
        {
            const int vpsSize = 12;

            if (byteSegment.Count < vpsSize)
                return;

            ProcessParameters(byteSegment, hasStartMarker, vpsSize - 1, _vpsMap);
        }

        private void ParseSps(ArraySegment<byte> byteSegment, bool hasStartMarker)
        {

        }

        private void ParsePps(ArraySegment<byte> byteSegment, bool hasStartMarker)
        {

        }

        private void ProcessParameters(ArraySegment<byte> byteSegment, bool hasStartMarker, int offset,
            Dictionary<int, byte[]> idToBytesMap)
        {
            _bitStreamReader.ReInitialize(hasStartMarker
                ? byteSegment.SubSegment(RawH265Frame.StartMarkerSize + offset)
                : byteSegment.SubSegment(offset));

            int id = _bitStreamReader.ReadUe();



        }
    }
}
