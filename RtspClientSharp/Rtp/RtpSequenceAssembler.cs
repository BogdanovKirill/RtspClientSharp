using System;
using System.Collections.Generic;
using RtspClientSharp.Utils;

namespace RtspClientSharp.Rtp
{
    class RtpSequenceAssembler : IRtpSequenceAssembler
    {
        private readonly ChunksArray _chunksArray;
        private readonly int _maxCorrectionLength;
        private ushort _previousCorrectSeqNumber;
        private readonly List<RtpPacket> _bufferedRtpPackets;
        private readonly List<int> _removeList;
        private bool _isFirstPacket = true;

        public RefAction<RtpPacket> PacketPassed { get; set; }

        public RtpSequenceAssembler(int maxRtpPacketSize, int maxCorrectionLength)
        {
            if (maxRtpPacketSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxRtpPacketSize));
            if (maxCorrectionLength < 1)
                throw new ArgumentOutOfRangeException(nameof(maxCorrectionLength));

            _maxCorrectionLength = maxCorrectionLength;
            _chunksArray = new ChunksArray(maxRtpPacketSize, maxCorrectionLength);

            _bufferedRtpPackets = new List<RtpPacket>(maxCorrectionLength);
            _removeList = new List<int>(maxCorrectionLength);
        }

        public void ProcessPacket(ref RtpPacket rtpPacket)
        {
            if (_isFirstPacket)
            {
                _previousCorrectSeqNumber = rtpPacket.SeqNumber;
                PacketPassed?.Invoke(ref rtpPacket);
                _isFirstPacket = false;
                return;
            }

            int delta = (ushort) (rtpPacket.SeqNumber - _previousCorrectSeqNumber);

            if (delta == 1)
            {
                _previousCorrectSeqNumber = rtpPacket.SeqNumber;
                PacketPassed?.Invoke(ref rtpPacket);

                if (_bufferedRtpPackets.Count == 0)
                    return;

                ushort nextSeqNumber = (ushort) (_previousCorrectSeqNumber + 1);
                ProcessBufferedPackets(nextSeqNumber);
                return;
            }

            if (delta > _maxCorrectionLength)
            {
                while (_bufferedRtpPackets.Count != 0)
                    PassNearestBufferedPacket();

                _previousCorrectSeqNumber = rtpPacket.SeqNumber;
                PacketPassed?.Invoke(ref rtpPacket);
            }
            else
            {
                if (rtpPacket.SeqNumber == _previousCorrectSeqNumber)
                    return;

                _bufferedRtpPackets.Add(rtpPacket);
                _chunksArray.Add(rtpPacket.PayloadSegment);

                if (_bufferedRtpPackets.Count != _maxCorrectionLength)
                    return;

                PassNearestBufferedPacket();

                ushort nextSeqNumber = (ushort) (_previousCorrectSeqNumber + 1);
                ProcessBufferedPackets(nextSeqNumber);
            }
        }

        private void PassNearestBufferedPacket()
        {
            int nearestIndex = 0;
            int nearestDelta = (ushort) (_bufferedRtpPackets[0].SeqNumber - _previousCorrectSeqNumber);

            for (int i = 1; i < _bufferedRtpPackets.Count; i++)
            {
                int delta = (ushort) (_bufferedRtpPackets[i].SeqNumber - _previousCorrectSeqNumber);

                if (delta < nearestDelta)
                {
                    nearestIndex = i;
                    nearestDelta = delta;
                }
            }

            RtpPacket nearestRtpPacket = _bufferedRtpPackets[nearestIndex];
            nearestRtpPacket.PayloadSegment = _chunksArray[nearestIndex];

            _previousCorrectSeqNumber = _bufferedRtpPackets[nearestIndex].SeqNumber;
            PacketPassed?.Invoke(ref nearestRtpPacket);

            _bufferedRtpPackets.RemoveAt(nearestIndex);
            _chunksArray.RemoveAt(nearestIndex);
        }

        private void ProcessBufferedPackets(ushort nextSeqNumber)
        {
            while (true)
            {
                bool anythingFound = false;

                for (int i = 0; i < _bufferedRtpPackets.Count; i++)
                {
                    if (_bufferedRtpPackets[i].SeqNumber != nextSeqNumber)
                        continue;

                    RtpPacket nextRtpPacket = _bufferedRtpPackets[i];
                    nextRtpPacket.PayloadSegment = _chunksArray[i];

                    _previousCorrectSeqNumber = nextRtpPacket.SeqNumber;
                    PacketPassed?.Invoke(ref nextRtpPacket);

                    ++nextSeqNumber;
                    anythingFound = true;
                    _removeList.Add(i);
                }

                if (!anythingFound)
                    break;
            }

            if (_removeList.Count == 0)
                return;

            if (_removeList.Count == _bufferedRtpPackets.Count)
            {
                _bufferedRtpPackets.Clear();
                _chunksArray.Clear();
            }
            else
            {
                if (_removeList.Count == 1)
                {
                    int removeIndex = _removeList[0];

                    _bufferedRtpPackets.RemoveAt(removeIndex);
                    _chunksArray.RemoveAt(removeIndex);
                }
                else
                {
                    _removeList.Sort();

                    for (int i = _removeList.Count - 1; i > -1; i--)
                    {
                        int removeIndex = _removeList[i];

                        _bufferedRtpPackets.RemoveAt(removeIndex);
                        _chunksArray.RemoveAt(removeIndex);
                    }
                }
            }

            _removeList.Clear();
        }
    }
}