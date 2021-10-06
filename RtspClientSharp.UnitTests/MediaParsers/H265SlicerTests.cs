using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.MediaParsers;
using System;
using System.Collections.Generic;
using System.Text;

namespace RtspClientSharp.UnitTests.MediaParsers
{
    [TestClass]
    public class H265SlicerTests
    {
        [TestMethod]
        public void Slice_OneEmptyNalUnit_NalUnitHandlerIsNotCalled()
        {
            var testBytes = new byte[] { 0, 0, 0, 1 };
            var testSegment = new ArraySegment<byte>(testBytes);

            bool nalUnitFound = false;
            H265Slicer.Slice(testSegment, s => nalUnitFound = true);

            Assert.IsFalse(nalUnitFound);
        }

        [TestMethod]
        public void Slice_NalUnitType19ThenOtherType_NalUnitHandlerCalledOnce()
        {
            // Validating nal unit type = IDR_W_RADL
            var testBytes = new byte[]
            {
                0x0, 0x0, 0x0, 0x1, 0x26, 0x1, 0x10, 0x9A, 0x87, 0xFD,
                0x0, 0x0, 0x0, 0x1, 0x6, 0x1, 0x2, 0x3, 0x4, 0x5
            };

            var testSegment = new ArraySegment<byte>(testBytes);

            int count = 0;
            H265Slicer.Slice(testSegment, s => ++count);

            Assert.AreEqual(1, count);
        }

        [TestMethod]
        public void Slice_NalUnitType1ThenOtherType_NalUnitHandlerCalledOnce()
        {
            // Validating nal unit type = TRAIL_R
            var testBytes = new byte[]
            {
                0x0, 0x0, 0x0, 0x1, 0x2, 0x1, 0xB, 0x40, 0x0, 0x17,
                0x0, 0x0, 0x0, 0x1, 0x6, 0x1, 0x2, 0x3, 0x4, 0x5
            };

            var testSegment = new ArraySegment<byte>(testBytes);

            int count = 0;
            H265Slicer.Slice(testSegment, s => ++count);

            Assert.AreEqual(1, count);
        }

        [TestMethod]
        public void Slice_NalUnitType32Then33Then34_NalUnitHandlerCalledThreeTimes()
        {
            // Validating the VPS, SPS and PPS bytes
            var testBytes = new byte[]
            {
                0x0, 0x0, 0x0, 0x1, 0x40, 0x1, 
                0x0, 0x0, 0x0, 0x1, 0x42, 0x1,
                0x0, 0x0, 0x0, 0x1, 0x4E, 0x1
            };

            var testSegment = new ArraySegment<byte>(testBytes);

            int count = 0;
            H265Slicer.Slice(testSegment, s => ++count);

            Assert.AreEqual(3, count);
        }
    }
}
