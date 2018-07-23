using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Rtp;

namespace RtspClientSharp.UnitTests.Rtp
{
    [TestClass]
    public class RtpSequenceAssemblerTests
    {
        private sealed class RtpPacketEqualityComparer : IEqualityComparer<RtpPacket>
        {
            public bool Equals(RtpPacket x, RtpPacket y)
            {
                return x.ProtocolVersion == y.ProtocolVersion &&
                       x.PaddingFlag == y.PaddingFlag &&
                       x.ExtensionFlag == y.ExtensionFlag &&
                       x.CsrcCount == y.CsrcCount &&
                       x.MarkerBit == y.MarkerBit &&
                       x.PayloadType == y.PayloadType &&
                       x.SeqNumber == y.SeqNumber &&
                       x.Timestamp == y.Timestamp &&
                       x.SyncSourceId == y.SyncSourceId &&
                       x.ExtensionHeaderId == y.ExtensionHeaderId &&
                       x.PayloadSegment.SequenceEqual(y.PayloadSegment);
            }

            public int GetHashCode(RtpPacket obj)
            {
                unchecked
                {
                    var hashCode = obj.ProtocolVersion;
                    hashCode = (hashCode * 397) ^ obj.PaddingFlag.GetHashCode();
                    hashCode = (hashCode * 397) ^ obj.ExtensionFlag.GetHashCode();
                    hashCode = (hashCode * 397) ^ obj.CsrcCount;
                    hashCode = (hashCode * 397) ^ obj.MarkerBit.GetHashCode();
                    hashCode = (hashCode * 397) ^ obj.PayloadType;
                    hashCode = (hashCode * 397) ^ obj.SeqNumber.GetHashCode();
                    hashCode = (hashCode * 397) ^ (int)obj.Timestamp;
                    hashCode = (hashCode * 397) ^ (int)obj.SyncSourceId;
                    hashCode = (hashCode * 397) ^ obj.ExtensionHeaderId;
                    hashCode = (hashCode * 397) ^ obj.PayloadSegment.GetHashCode();
                    return hashCode;
                }
            }
        }

        private static readonly Random Random = new Random();

        [TestMethod]
        public void ProcessPacket_SeveralPacketsWithValidOrder_PacketPassedImmediatelyCalled()
        {
            var testPayloadSegment = new ArraySegment<byte>(new byte[1024]);
            int testPacketsCount = 10;
            int countPassed = 0;

            var assembler = new RtpSequenceAssembler(1500, testPacketsCount);
            assembler.PacketPassed += (ref RtpPacket packet) => ++countPassed;
            for (ushort i = 0; i < testPacketsCount; i++)
            {
                var testPacket = new RtpPacket(i, testPayloadSegment);
                assembler.ProcessPacket(ref testPacket);
            }

            Assert.AreEqual(testPacketsCount, countPassed);
        }

        [TestMethod]
        [DataRow(2)]
        [DataRow(4)]
        [DataRow(8)]
        [DataRow(16)]
        [DataRow(32)]
        [DataRow(1024)]
        public void ProcessPacket_SeveralPacketsWithWrongOrder_SequenceIsRestored(int testPacketsCount)
        {
            RtpPacket packet;
            List<RtpPacket> testPacketsList = CreateTestPacketsList(testPacketsCount);
            var shuffledList = testPacketsList.ToList();
            ShuffleList(shuffledList);
            var resultList = new List<RtpPacket>();
            
            var assembler = new RtpSequenceAssembler(1500, testPacketsCount);
            packet = new RtpPacket(ushort.MaxValue, Array.Empty<byte>());
            assembler.ProcessPacket(ref packet);
            assembler.PacketPassed += (ref RtpPacket p) => resultList.Add(new RtpPacket(p.SeqNumber, p.PayloadSegment.ToArray()));
            for (int i = 0; i < shuffledList.Count; i++)
            {
                packet = shuffledList[i];
                assembler.ProcessPacket(ref packet);
            }

            Assert.IsTrue(testPacketsList.SequenceEqual(resultList, new RtpPacketEqualityComparer()));
        }

        [TestMethod]
        [DataRow(8, 1)]
        [DataRow(16, 1)]
        [DataRow(32, 1)]
        [DataRow(1024, 1)]
        [DataRow(8, 2)]
        [DataRow(16, 2)]
        [DataRow(32, 2)]
        [DataRow(1024, 2)]
        [DataRow(8, 4)]
        [DataRow(16, 4)]
        [DataRow(32, 4)]
        [DataRow(1024, 4)]
        public void ProcessPacket_OnePacketIsLost_SequenceIsOrdered(int testPacketsCount, int maxCorrectionLength)
        {
            RtpPacket packet;
            List<RtpPacket> testPacketsList = CreateTestPacketsList(testPacketsCount);
            int removeIndex = testPacketsCount / 2;
            //remove one packet
            testPacketsList.RemoveAt(removeIndex / 2);
            List<RtpPacket> testPacketsLisWithShuffle = testPacketsList.ToList();
            //additionally swap next two packets
            packet = testPacketsLisWithShuffle[removeIndex];
            testPacketsLisWithShuffle[removeIndex] = testPacketsLisWithShuffle[removeIndex + 1];
            testPacketsLisWithShuffle[removeIndex + 1] = packet;

            var resultList = new List<RtpPacket>();

            var assembler = new RtpSequenceAssembler(1500, 8);
            packet = new RtpPacket(ushort.MaxValue, Array.Empty<byte>());
            assembler.ProcessPacket(ref packet);
            assembler.PacketPassed += (ref RtpPacket p) => resultList.Add(new RtpPacket(p.SeqNumber, p.PayloadSegment.ToArray()));
            for (int i = 0; i < testPacketsLisWithShuffle.Count; i++)
            {
                packet = testPacketsLisWithShuffle[i];
                assembler.ProcessPacket(ref packet);
            }

            Assert.IsTrue(testPacketsList.Take(resultList.Count).SequenceEqual(resultList, new RtpPacketEqualityComparer()));
        }
        
        private static List<RtpPacket> CreateTestPacketsList(int testPacketsCount)
        {
            var testPacketsList = new List<RtpPacket>();

            for (ushort i = 0; i < testPacketsCount; i++)
            {
                var randomBytes = new byte[Random.Next(100, 1500)];
                Random.NextBytes(randomBytes);

                testPacketsList.Add(new RtpPacket(i, randomBytes));
            }

            return testPacketsList;
        }

        private static void ShuffleList<T>(IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Random.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}
