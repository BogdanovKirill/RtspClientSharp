using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.MediaParsers;

namespace RtspClientSharp.UnitTests.MediaParsers
{
    [TestClass]
    public class H264SlicerTests
    {
        [TestMethod]
        public void Slice_OneEmptyNalUnit_NalUnitHandlerIsNotCalled()
        {
            var testBytes = new byte[] {0, 0, 0, 1};
            var testSegment = new ArraySegment<byte>(testBytes);

            bool nalUnitFound = false;
            H264Slicer.Slice(testSegment, s => nalUnitFound = true);

            Assert.IsFalse(nalUnitFound);
        }

        [TestMethod]
        public void Slice_NalUnitType5ThenOtherType_NalUnitHandlerCalledOnce()
        {
            var testBytes = new byte[]
            {
                0x0, 0x0, 0x0, 0x1, 0x65, 0x1, 0x2, 0x3, 0x4, 0x5,
                0x0, 0x0, 0x0, 0x1, 0x6, 0x1, 0x2, 0x3, 0x4, 0x5
            };

            var testSegment = new ArraySegment<byte>(testBytes);

            int count = 0;
            H264Slicer.Slice(testSegment, s => ++count);

            Assert.AreEqual(1, count);
        }

        [TestMethod]
        public void Slice_NalUnitType1ThenOtherType_NalUnitHandlerCalledOnce()
        {
            var testBytes = new byte[]
            {
                0x0, 0x0, 0x0, 0x1, 0x61, 0x1, 0x2, 0x3, 0x4, 0x5,
                0x0, 0x0, 0x0, 0x1, 0x6, 0x1, 0x2, 0x3, 0x4, 0x5
            };

            var testSegment = new ArraySegment<byte>(testBytes);

            int count = 0;
            H264Slicer.Slice(testSegment, s => ++count);

            Assert.AreEqual(1, count);
        }

        [TestMethod]
        public void Slice_NalUnitType7Then8Then5_NalUnitHandlerCalledThreeTimes()
        {
            var testBytes = new byte[]
            {
                0x0, 0x0, 0x0, 0x1, 0x67, 0x1, 0x0, 0x0, 0x0, 0x1, 0x68, 0x2,
                0x0, 0x0, 0x0, 0x1, 0x65, 0x1, 0x2, 0x3, 0x4, 0x5
            };

            var testSegment = new ArraySegment<byte>(testBytes);

            int count = 0;
            H264Slicer.Slice(testSegment, s => ++count);

            Assert.AreEqual(3, count);
        }
    }
}