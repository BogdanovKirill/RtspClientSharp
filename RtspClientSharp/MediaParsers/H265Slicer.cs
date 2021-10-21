using Logger;
using RtspClientSharp.RawFrames.Video;
using RtspClientSharp.Rtp;
using RtspClientSharp.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace RtspClientSharp.MediaParsers
{
    static class H265Slicer
    {
        public static void Slice(ArraySegment<byte> byteSegment, Action<ArraySegment<byte>> nalUnitHandler)
        {
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");
            Debug.Assert(ArrayUtils.StartsWith(byteSegment.Array, byteSegment.Offset, byteSegment.Count, 
                RawH265Frame.StartMarker));

            int endIndex = byteSegment.Offset + byteSegment.Count;

            int nalUnitStartIndex = ArrayUtils.IndexOfBytes(byteSegment.Array, RawH265Frame.StartMarker, 
                byteSegment.Offset, byteSegment.Count);

            if (nalUnitStartIndex == -1)
                nalUnitHandler?.Invoke(byteSegment);

            while (true)
            {
                int tailLength = endIndex - nalUnitStartIndex;

                if (tailLength == RawH265Frame.StartMarkerSize)
                    return;
                
                int nalUnitType = (byteSegment.Array[nalUnitStartIndex + RawH265Frame.StartMarkerSize] >> 1) & 0x3F;

                if (!RtpH265TypeUtils.CheckIfIsValid(nalUnitType))
                    throw new H265ParserException($"Invalid (HEVC) NAL Unit Type { nalUnitType }");

                if ((RtpH265NALUType)nalUnitType == RtpH265NALUType.IDR_W_RADL || (RtpH265NALUType)nalUnitType == RtpH265NALUType.TRAIL_R)
                {
                    nalUnitHandler?.Invoke(new ArraySegment<byte>(byteSegment.Array, nalUnitStartIndex, tailLength));
                    return;
                }

                int nextNalUnitStartIndex = ArrayUtils.IndexOfBytes(byteSegment.Array, RawH265Frame.StartMarker, 
                    nalUnitStartIndex + RawH265Frame.StartMarkerSize, tailLength - RawH265Frame.StartMarkerSize);

                if(nextNalUnitStartIndex > 0)
                {
                    int nalUnitLength = nextNalUnitStartIndex - nalUnitStartIndex;

                    if (nalUnitLength != RawH265Frame.StartMarkerSize)
                        nalUnitHandler?.Invoke(new ArraySegment<byte>(byteSegment.Array, nalUnitStartIndex, nalUnitLength));
                }
                else
                {
                    nalUnitHandler?.Invoke(new ArraySegment<byte>(byteSegment.Array, nalUnitStartIndex, tailLength));
                    return;
                }

                nalUnitStartIndex = nextNalUnitStartIndex;
            }
        }
    }
}
