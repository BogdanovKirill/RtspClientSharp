using RtspClientSharp.RawFrames;
using RtspClientSharp.RawFrames.Video;
using RtspClientSharp.Rtp;
using RtspClientSharp.Utils;
using System;
using System.Diagnostics;

namespace RtspClientSharp.MediaParsers
{
    class H265Parser
    {
        public static readonly ArraySegment<byte> StartMarkSegment = new ArraySegment<byte>(RawH265Frame.StartMarker);

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

            RtpH265NALUType nalUnitType = (RtpH265NALUType)(byteSegment.Array[offset] & 0x1F);
            bool nri = (byteSegment.Array[offset] & 0x7E00) >> 9 == 0;

            if (!nri)
                return;


        }
    }
}
