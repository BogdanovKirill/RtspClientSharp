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
            Debug.Assert(ArrayUtils.StartsWith(byteSegment.Array, byteSegment.Offset, byteSegment.Count, RawH265Frame.StartMarker));

            int endIndex = byteSegment.Offset + byteSegment.Count;

            int nalUnitStartIndex = ArrayUtils.IndexOfBytes(byteSegment.Array, RawH265Frame.StartMarker, byteSegment.Offset, byteSegment.Count);

            while (true)
            {
                int tailLength = endIndex - nalUnitStartIndex;

                if (tailLength == RawH265Frame.StartMarkerSize)
                    return;

                RtpH265NALUType nalUnitType = (RtpH265NALUType)(byteSegment.Array[nalUnitStartIndex + RawH265Frame.StartMarkerSize] & 0x1F);

                if (nalUnitType == RtpH265NALUType.TSA_N)
                {
                    nalUnitHandler?.Invoke(new ArraySegment<byte>(byteSegment.Array, nalUnitStartIndex, tailLength));
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
