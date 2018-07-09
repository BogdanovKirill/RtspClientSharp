using System;
using System.Diagnostics;
using RtspClientSharp.RawFrames.Video;
using RtspClientSharp.Utils;

namespace RtspClientSharp.MediaParsers
{
    static class H264Slicer
    {
        public static void Slice(ArraySegment<byte> byteSegment, Action<ArraySegment<byte>> nalUnitHandler)
        {
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");
            Debug.Assert(ArrayUtils.StartsWith(byteSegment.Array, byteSegment.Offset, byteSegment.Count,
                RawH264Frame.StartMarker));

            int endIndex = byteSegment.Offset + byteSegment.Count;

            int nalUnitStartIndex = ArrayUtils.IndexOfBytes(byteSegment.Array, RawH264Frame.StartMarker,
                byteSegment.Offset, byteSegment.Count);

            if (nalUnitStartIndex == -1)
                nalUnitHandler?.Invoke(byteSegment);

            while (true)
            {
                int tailLength = endIndex - nalUnitStartIndex;

                if (tailLength == RawH264Frame.StartMarker.Length)
                    return;

                int nalUnitType = byteSegment.Array[nalUnitStartIndex + RawH264Frame.StartMarker.Length] & 0x1F;

                if (nalUnitType == 5 || nalUnitType == 1)
                {
                    nalUnitHandler?.Invoke(new ArraySegment<byte>(byteSegment.Array, nalUnitStartIndex, tailLength));
                    return;
                }

                int nextNalUnitStartIndex = ArrayUtils.IndexOfBytes(byteSegment.Array, RawH264Frame.StartMarker,
                    nalUnitStartIndex + RawH264Frame.StartMarker.Length, tailLength - RawH264Frame.StartMarker.Length);

                if (nextNalUnitStartIndex > 0)
                {
                    int nalUnitLength = nextNalUnitStartIndex - nalUnitStartIndex;

                    if (nalUnitLength != RawH264Frame.StartMarker.Length)
                        nalUnitHandler?.Invoke(new ArraySegment<byte>(byteSegment.Array, nalUnitStartIndex,
                            nalUnitLength));
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