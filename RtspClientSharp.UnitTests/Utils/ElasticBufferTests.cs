using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Utils;

namespace RtspClientSharp.UnitTests.Utils
{
    [TestClass]
    public class ElasticBufferTests
    {
        [TestMethod]
        public void AddBytes_TestSegment_HasValidStateSegmentAndCountDataPropertyShouldBeGreaterThanZero()
        {
            var testBytes = new byte[] {1, 2, 3, 4, 5, 6, 7, 8, 9};
            var testSegment = new ArraySegment<byte>(testBytes);

            var buffer = new ElasticBuffer(1, 16);
            buffer.AddBytes(testSegment);

            Assert.IsTrue(ArrayUtils.IsBytesEquals(testBytes, 0, testBytes.Length,
                buffer.StateByteSegment.Array, buffer.StateByteSegment.Offset, buffer.StateByteSegment.Count));
            Assert.IsTrue(buffer.CountData > 0);
        }

        [TestMethod]
        public void GetAccumulatedBytes_TestSegment_ReturnsValidSegmentAndResetsInternalState()
        {
            var testBytes = new byte[] {1, 2, 3, 4, 5, 6, 7, 8, 9};
            var testSegment = new ArraySegment<byte>(testBytes);

            var buffer = new ElasticBuffer(1, 16);
            buffer.AddBytes(testSegment);
            ArraySegment<byte> resultSegment = buffer.GetAccumulatedBytes();

            Assert.AreEqual(0, buffer.CountData);
            Assert.IsTrue(ArrayUtils.IsBytesEquals(testBytes, 0, testBytes.Length,
                resultSegment.Array, resultSegment.Offset, resultSegment.Count));
        }
    }
}