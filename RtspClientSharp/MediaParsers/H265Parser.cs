using Logger;
using RtspClientSharp.RawFrames;
using RtspClientSharp.RawFrames.Video;
using RtspClientSharp.Rtp;
using RtspClientSharp.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RtspClientSharp.MediaParsers
{
    class H265Parser
    {
        public static readonly ArraySegment<byte> StartMarkSegment = new ArraySegment<byte>(RawH265Frame.StartMarker);

        private readonly Func<DateTime> _frameTimestampProvider;
        private readonly BitStreamReader _bitStreamReader = new BitStreamReader();
        private readonly Dictionary<int, byte[]> _vpsMap = new Dictionary<int, byte[]>();
        private readonly Dictionary<int, byte[]> _spsMap = new Dictionary<int, byte[]>();
        private readonly Dictionary<int, byte[]> _ppsMap = new Dictionary<int, byte[]>();
        private readonly Dictionary<int, byte[]> _seiMap = new Dictionary<int, byte[]>();
        private byte[] _parametersBytes = new byte[0];
        private bool _waitForIFrame = true;
        private bool _updatedParametersBytes;
        private int _sliceType = -1;

        private readonly MemoryStream _frameStream;

        public Action<RawFrame> FrameGenerated;

        public H265Parser(Func<DateTime> frameTimestampProvider)
        {
            _frameTimestampProvider = frameTimestampProvider ?? throw new ArgumentException(nameof(frameTimestampProvider));
            _frameStream = new MemoryStream(8 * 1024);
        }

        public void Parse(ArraySegment<byte> byteSegment, bool generateFrame)
        {
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");

            if (ArrayUtils.StartsWith(byteSegment.Array, byteSegment.Offset, byteSegment.Count,
                RawH265Frame.StartMarker))
                H265Slicer.Slice(byteSegment, SliceOnNalUnitFound);
            else
                ProcessNalUnit(byteSegment, false, ref generateFrame);

            if (generateFrame)
                TryGetFrameBytes();
        }

        public void TryGetFrameBytes()
        {
            if (_frameStream.Position == 0)
                return;

            var frameBytes = new ArraySegment<byte>(_frameStream.GetBuffer(), 0, (int)_frameStream.Position);
            _frameStream.Position = 0;
            TryGenerateFrame(frameBytes);
        }

        private void TryGenerateFrame(ArraySegment<byte> frameBytes)
        {
            if (_updatedParametersBytes)
            {
                UpdateParametersBytes();
                _updatedParametersBytes = false;
            }

            if (_sliceType == -1 || _parametersBytes.Length == 0)
                return;

            HevcFrameType frameType = GetFrameType(_sliceType);
            //PlayerLogger.fLogMethod($"TryGenerateFrame frameType { frameType }");
            //PlayerLogger.fLogMethod($"TryGenerateFrame frameBytesCount { frameBytes.Count }");

            if (frameType == HevcFrameType.Unknown)
            {
                //PlayerLogger.fLogMethod($"Unknown frame sliceType { _sliceType }");
            }

            _sliceType = -1;
            DateTime frameTimestamp;

            if (frameType == HevcFrameType.PredictionFrame && !_waitForIFrame)
            {
                frameTimestamp = _frameTimestampProvider();
                FrameGenerated?.Invoke(new RawH265PFrame(frameTimestamp, frameBytes));
                return;
            }

            if (frameType != HevcFrameType.IntraFrame)
                return;

            _waitForIFrame = false;
            var parametersBytesSegment = new ArraySegment<byte>(_parametersBytes);

            frameTimestamp = _frameTimestampProvider();
            FrameGenerated?.Invoke(new RawH265IFrame(frameTimestamp, frameBytes, parametersBytesSegment));
        }

        public void ResetState()
        {
            _frameStream.Position = 0;
            _sliceType = -1;
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
                throw new H265ParserException($"Forbidden zero bit's different than zero.");

            int nalUnitType = (byteSegment.Array[offset] >> 1) & 0x3F;
            int layerId = ((byteSegment.Array[offset] << 5) & 0x20) | ((byteSegment.Array[offset + 1] >> 3) & 0x1F);
            int temporalId = byteSegment.Array[offset + 1] & 0x07;

            //Required to be equal to zero
            if (layerId != 0)
                throw new H265ParserException($"Invalid LayerId { layerId }.");

            //The value of TemporalId is equal to TID minus 1. A TID value of 0 is illegal...
            if (temporalId == 0)
                throw new H265ParserException($"Invalid TemporalId (TID) { temporalId }.");

            //Checking Nal unit type
            if (!RtpH265TypeUtils.CheckIfIsValid(nalUnitType))
                throw new H265ParserException($"Invalid (HEVC) NAL Unit Type { nalUnitType }.");

            switch ((RtpH265NALUType)nalUnitType)
            {
                /* Video parameter set */
                case RtpH265NALUType.VPS_NUT:
                    ProcessParameters(byteSegment, hasStartMarker, 0, _vpsMap);
                    return;
                /* Sequence parameter set */
                case RtpH265NALUType.SPS_NUT:
                    ProcessParameters(byteSegment, hasStartMarker, 0, _spsMap);
                    return;
                /* Picture parameter set */
                case RtpH265NALUType.PPS_NUT:
                    ProcessParameters(byteSegment, hasStartMarker, 0, _ppsMap);
                    return;
                /* Supplemental enhancement information (SEI)*/
                case RtpH265NALUType.PREFIX_SEI_NUT:
                    ProcessParameters(byteSegment, hasStartMarker, 0, _seiMap);
                    return;
                default:
                    break;
            }

            if (_sliceType == -1 && ((RtpH265NALUType)nalUnitType == RtpH265NALUType.TRAIL_R || (RtpH265NALUType)nalUnitType == RtpH265NALUType.IDR_W_RADL))
                _sliceType = GetSliceType(byteSegment, hasStartMarker);

            if (generateFrame && (hasStartMarker || byteSegment.Offset >= StartMarkSegment.Count) && _frameStream.Position == 0)
            {
                if (!hasStartMarker)
                {
                    int newOffset = byteSegment.Offset - StartMarkSegment.Count;

                    Buffer.BlockCopy(StartMarkSegment.Array, StartMarkSegment.Offset,
                        byteSegment.Array, newOffset, StartMarkSegment.Count);

                    byteSegment = new ArraySegment<byte>(byteSegment.Array, newOffset, byteSegment.Count + StartMarkSegment.Count);
                }

                generateFrame = false;
                TryGenerateFrame(byteSegment);
            }
            else
            {
                if (!hasStartMarker)
                    _frameStream.Write(StartMarkSegment.Array, StartMarkSegment.Offset, StartMarkSegment.Count);

                _frameStream.Write(byteSegment.Array, byteSegment.Offset, byteSegment.Count);
            }
        }

        private void ProcessParameters(ArraySegment<byte> byteSegment, bool hasStartMarker, int offset,
            Dictionary<int, byte[]> idToBytesMap)
        {
            _bitStreamReader.ReInitialize(hasStartMarker
                ? byteSegment.SubSegment(RawH265Frame.StartMarkerSize + offset)
                : byteSegment.SubSegment(offset));

            int id = _bitStreamReader.ReadUe();

            if (id == -1)
                return;

            if (hasStartMarker)
                byteSegment = byteSegment.SubSegment(RawH265Frame.StartMarkerSize);

            if (TryUpdateParameters(byteSegment, id, idToBytesMap))
                _updatedParametersBytes = true;

        }

        private static bool TryUpdateParameters(ArraySegment<byte> byteSegment, int id,
            Dictionary<int, byte[]> idToBytesMap)
        {
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");

            if (!idToBytesMap.TryGetValue(id, out byte[] data))
            {
                data = new byte[byteSegment.Count];
                Buffer.BlockCopy(byteSegment.Array, byteSegment.Offset, data, 0, byteSegment.Count);
                idToBytesMap.Add(id, data);
                return true;
            }

            if (!ArrayUtils.IsBytesEquals(data, 0, data.Length, byteSegment.Array, byteSegment.Offset,
                byteSegment.Count))
            {
                if (data.Length != byteSegment.Count)
                    data = new byte[byteSegment.Count];

                Buffer.BlockCopy(byteSegment.Array, byteSegment.Offset, data, 0, byteSegment.Count);
                idToBytesMap[id] = data;
                return true;
            }

            return false;
        }

        private void UpdateParametersBytes()
        {
            int totalSize = _vpsMap.Values.Sum(vps => vps.Length) + _spsMap.Values.Sum(sps => sps.Length) +
                _ppsMap.Values.Sum(pps => pps.Length) + _seiMap.Values.Sum(sei => sei.Length) +
                RawH265Frame.StartMarkerSize *
                (_vpsMap.Count + _spsMap.Count + _ppsMap.Count + _seiMap.Count);

            if (_parametersBytes.Length != totalSize)
                _parametersBytes = new byte[totalSize];

            int offset = 0;

            CopyToParametersBytes(_vpsMap.Values, ref offset);
            CopyToParametersBytes(_spsMap.Values, ref offset);
            CopyToParametersBytes(_ppsMap.Values, ref offset);
            CopyToParametersBytes(_seiMap.Values, ref offset);
        }

        private void CopyToParametersBytes(Dictionary<int, byte[]>.ValueCollection idToBytesMap, ref int offset)
        {
            foreach (byte[] param in idToBytesMap)
            {
                Buffer.BlockCopy(RawH265Frame.StartMarker, 0, _parametersBytes, offset, RawH265Frame.StartMarkerSize);
                offset += RawH265Frame.StartMarkerSize;
                Buffer.BlockCopy(param, 0, _parametersBytes, offset, param.Length);
                offset += param.Length;
            }
        }

        private int GetSliceType(ArraySegment<byte> byteSegment, bool hasStartMarker)
        {
            int offset = 1;

            if (hasStartMarker)
                offset += RawH265Frame.StartMarkerSize;

            _bitStreamReader.ReInitialize(byteSegment.SubSegment(offset));

            int firstMbInSlice = _bitStreamReader.ReadUe();

            if (firstMbInSlice == -1)
                return firstMbInSlice;

            int nalSliceType = _bitStreamReader.ReadUe();
            return nalSliceType;
        }

        private static HevcFrameType GetFrameTypeFromNalUnitType(int nalUnitType)
        {
            if ((RtpH265NALUType)nalUnitType == RtpH265NALUType.IDR_W_RADL)
                return HevcFrameType.IntraFrame;
            if ((RtpH265NALUType)nalUnitType == RtpH265NALUType.TRAIL_R)
                return HevcFrameType.PredictionFrame;

            return HevcFrameType.Unknown;
        }

        private static HevcFrameType GetFrameType(int sliceType)
        {
            if (sliceType == 0 || sliceType == 5)
                return HevcFrameType.PredictionFrame;
            if (sliceType == 1 || sliceType == 2 || sliceType == 14)
                return HevcFrameType.IntraFrame;

            return HevcFrameType.Unknown;
        }
    }
}
