using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RtspClientSharp.RawFrames;
using RtspClientSharp.RawFrames.Video;
using RtspClientSharp.Utils;

namespace RtspClientSharp.MediaParsers
{
    class H264Parser
    {
        private enum FrameType
        {
            Unknown,
            IntraFrame,
            PredictionFrame
        }

        public static readonly ArraySegment<byte> StartMarkerSegment = new ArraySegment<byte>(RawH264Frame.StartMarker);

        private readonly BitStreamReader _bitStreamReader = new BitStreamReader();
        private readonly Dictionary<int, byte[]> _spsMap = new Dictionary<int, byte[]>();
        private readonly Dictionary<int, byte[]> _ppsMap = new Dictionary<int, byte[]>();
        private bool _waitForIFrame = true;
        private byte[] _spsPpsBytes = new byte[0];
        private bool _updateSpsPpsBytes;
        private int _sliceType = -1;

        private readonly ElasticBuffer _frameBuffer;

        public Action<RawFrame> FrameGenerated;

        public H264Parser()
        {
            _frameBuffer = new ElasticBuffer(8 * 1024, 8 * 1024 * 1024);
        }

        public void Parse(DateTime frameTimestamp, ArraySegment<byte> byteSegment, bool dontSliceForce,
            bool generateFrame)
        {
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");

            if (ArrayUtils.StartsWith(byteSegment.Array, byteSegment.Offset, byteSegment.Count,
                RawH264Frame.StartMarker))
            {
                if (dontSliceForce)
                    ProcessNalUnit(frameTimestamp, byteSegment, true, generateFrame);
                else
                    H264Slicer.Slice(byteSegment, SlicerOnNalUnitFound);
            }
            else
                ProcessNalUnit(frameTimestamp, byteSegment, false, false);

            if (generateFrame && _frameBuffer.CountData != 0)
            {
                ArraySegment<byte> frameBytes = _frameBuffer.GetAccumulatedBytes();
                TryGenerateFrame(frameTimestamp, frameBytes);
            }
        }

        private void TryGenerateFrame(DateTime frameTimestamp, ArraySegment<byte> frameBytes)
        {
            if (_updateSpsPpsBytes)
            {
                UpdateSpsPpsBytes();
                _updateSpsPpsBytes = false;
            }

            if (_sliceType == -1 || _spsPpsBytes.Length == 0)
                return;

            FrameType frameType = GetFrameType(_sliceType);
            _sliceType = -1;

            if (frameType == FrameType.PredictionFrame && !_waitForIFrame)
            {
                FrameGenerated?.Invoke(new RawH264PFrame(frameTimestamp, frameBytes));
                return;
            }

            if (frameType == FrameType.IntraFrame)
            {
                _waitForIFrame = false;
                var byteSegment = new ArraySegment<byte>(_spsPpsBytes);
                FrameGenerated?.Invoke(new RawH264IFrame(frameTimestamp, frameBytes, byteSegment));
            }
        }

        public void ResetState()
        {
            _frameBuffer.ResetState();
            _sliceType = -1;
            _waitForIFrame = true;
        }

        private void SlicerOnNalUnitFound(ArraySegment<byte> byteSegment)
        {
            ProcessNalUnit(DateTime.MinValue, byteSegment, true, false);
        }

        private void ProcessNalUnit(DateTime frameTimestamp, ArraySegment<byte> byteSegment, bool hasStartMarker,
            bool generateFrame)
        {
            Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");

            int offset = byteSegment.Offset;

            if (hasStartMarker)
                offset += RawH264Frame.StartMarker.Length;

            int nalUnitType = byteSegment.Array[offset] & 0x1F;
            bool nri = ((byteSegment.Array[offset] >> 5) & 3) == 0;

            if (!(nalUnitType > 0 && nalUnitType < 24))
                throw new H264ParserException($"Invalid nal unit type: {nalUnitType}");

            if (nalUnitType == 7)
            {
                ParseSps(byteSegment, hasStartMarker);
                return;
            }

            if (nalUnitType == 8)
            {
                ParsePps(byteSegment, hasStartMarker);
                return;
            }

            if (_sliceType == -1 && (nalUnitType == 5 || nalUnitType == 1))
                _sliceType = GetSliceType(byteSegment, hasStartMarker);

            if (nri || nalUnitType == 6)
                return;

            if (generateFrame && _frameBuffer.CountData == 0)
                TryGenerateFrame(frameTimestamp, byteSegment);
            else
            {
                if (!hasStartMarker)
                    _frameBuffer.AddBytes(StartMarkerSegment);

                _frameBuffer.AddBytes(byteSegment);
            }
        }

        private void ParseSps(ArraySegment<byte> byteSegment, bool hasStartMarker)
        {
            const int spsMinSize = 5;

            if (byteSegment.Count < spsMinSize)
                return;

            ProcessSpsOrPps(byteSegment, hasStartMarker, spsMinSize - 1, _spsMap);
        }

        private void ParsePps(ArraySegment<byte> byteSegment, bool hasStartMarker)
        {
            const int ppsMinSize = 2;

            if (byteSegment.Count < ppsMinSize)
                return;

            ProcessSpsOrPps(byteSegment, hasStartMarker, ppsMinSize - 1, _ppsMap);
        }

        private void ProcessSpsOrPps(ArraySegment<byte> byteSegment, bool hasStartMarker, int offset,
            Dictionary<int, byte[]> idToBytesMap)
        {
            _bitStreamReader.ReInitialize(hasStartMarker
                ? byteSegment.SubSegment(RawH264Frame.StartMarker.Length + offset)
                : byteSegment.SubSegment(offset));

            int id = _bitStreamReader.ReadUe();

            if (id == -1)
                return;

            if (hasStartMarker)
                byteSegment = byteSegment.SubSegment(RawH264Frame.StartMarker.Length);

            if (TryUpdateSpsOrPps(byteSegment, id, idToBytesMap))
                _updateSpsPpsBytes = true;
        }

        private static bool TryUpdateSpsOrPps(ArraySegment<byte> byteSegment, int id,
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

        private void UpdateSpsPpsBytes()
        {
            int totalSize = _spsMap.Values.Sum(sps => sps.Length) + _ppsMap.Values.Sum(pps => pps.Length) +
                            RawH264Frame.StartMarker.Length * (_spsMap.Count + _ppsMap.Count);

            if (_spsPpsBytes.Length != totalSize)
                _spsPpsBytes = new byte[totalSize];

            int offset = 0;

            foreach (byte[] sps in _spsMap.Values)
            {
                Buffer.BlockCopy(RawH264Frame.StartMarker, 0, _spsPpsBytes, offset, RawH264Frame.StartMarker.Length);
                offset += RawH264Frame.StartMarker.Length;
                Buffer.BlockCopy(sps, 0, _spsPpsBytes, offset, sps.Length);
                offset += sps.Length;
            }

            foreach (byte[] pps in _ppsMap.Values)
            {
                Buffer.BlockCopy(RawH264Frame.StartMarker, 0, _spsPpsBytes, offset, RawH264Frame.StartMarker.Length);
                offset += RawH264Frame.StartMarker.Length;
                Buffer.BlockCopy(pps, 0, _spsPpsBytes, offset, pps.Length);
                offset += pps.Length;
            }
        }

        private int GetSliceType(ArraySegment<byte> byteSegment, bool hasStartMarker)
        {
            int offset = 1;

            if (hasStartMarker)
                offset += RawH264Frame.StartMarker.Length;

            _bitStreamReader.ReInitialize(byteSegment.SubSegment(offset));

            int firstMbInSlice = _bitStreamReader.ReadUe();

            if (firstMbInSlice == -1)
                return firstMbInSlice;

            int nalSliceType = _bitStreamReader.ReadUe();
            return nalSliceType;
        }

        private static FrameType GetFrameType(int sliceType)
        {
            if (sliceType == 0 || sliceType == 5)
                return FrameType.PredictionFrame;
            if (sliceType == 2 || sliceType == 7)
                return FrameType.IntraFrame;

            return FrameType.Unknown;
        }
    }
}