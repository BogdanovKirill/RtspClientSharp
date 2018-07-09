using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.MediaParsers;

namespace RtspClientSharp.UnitTests.MediaParsers
{
    [TestClass]
    public class MediaPayloadParserTests
    {
        private class MediaPayloadParserFake : MediaPayloadParser
        {
            public new DateTime GetFrameTimestamp(TimeSpan timeOffset)
            {
                return base.GetFrameTimestamp(timeOffset);
            }

            public override void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit)
            {
            }

            public override void ResetState()
            {
            }
        }

        [TestMethod]
        public void GetFrameTimestamp_TwoTestTimeOffsets_ResultTimestampsShouldBeDifferentAndGreaterThanUtcNow()
        {
            var parser = new MediaPayloadParserFake();

            DateTime timestamp1 = parser.GetFrameTimestamp(TimeSpan.FromMilliseconds(40));
            DateTime timestamp2 = parser.GetFrameTimestamp(TimeSpan.FromMilliseconds(80));

            Assert.IsTrue(timestamp1 > DateTime.UtcNow);
            Assert.IsTrue(timestamp2 > timestamp1);
        }
    }
}