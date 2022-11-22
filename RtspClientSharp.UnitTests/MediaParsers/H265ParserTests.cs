using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.MediaParsers;
using RtspClientSharp.RawFrames.Video;
using System;
using System.Linq;

namespace RtspClientSharp.UnitTests.MediaParsers
{
    [TestClass]
    public class H265ParserTests
    {
        [TestMethod]
        public void Parse_IFrameBytesThenPFrameBytes_GenerateTwoFrames()
        {
            var vpsBytes = Convert.FromBase64String("QAEMAf//AWAAAAMAsAAAAwAAAwCZrAk=");
            var spsBytes = Convert.FromBase64String("QgEBAWAAAAMAsAAAAwAAAwCZoAFAIAWhY2uSTL03AQEBAIA=");
            var ppsBytes = Convert.FromBase64String("RAHA8vA8kA==");
            var iFrameBytes = new byte[] { 0x0, 0x0, 0x0, 0x1, 0x26, 0x01, 0xa0, 0x9a, 0x87 };
            var pFrameBytes = new byte[] { 0x0, 0x0, 0x0, 0x1, 0x2, 0x01, 0x0b, 0x40, 0x00 };

            RawH265Frame frame = null;
            var parser = new H265Parser(() => DateTime.UtcNow) { FrameGenerated = rawFrame => frame = (RawH265Frame)rawFrame };
            parser.Parse(new ArraySegment<byte>(vpsBytes), false);
            parser.Parse(new ArraySegment<byte>(spsBytes), false);
            parser.Parse(new ArraySegment<byte>(ppsBytes), false);
            parser.Parse(new ArraySegment<byte>(iFrameBytes), true);

            Assert.IsInstanceOfType(frame, typeof(RawH265IFrame));
            Assert.IsTrue(frame.FrameSegment.SequenceEqual(iFrameBytes));

            parser.Parse(new ArraySegment<byte>(pFrameBytes), true);

            Assert.IsInstanceOfType(frame, typeof(RawH265PFrame));
            Assert.IsTrue(frame.FrameSegment.SequenceEqual(pFrameBytes));
        }

        [TestMethod]
        public void ResetState_VpsPpsSpsThenIFrameThenReset_FrameGenerated()
        {
            var vpsBytes = Convert.FromBase64String("QAEMAf//AWAAAAMAsAAAAwAAAwCZrAk=");
            var spsBytes = Convert.FromBase64String("QgEBAWAAAAMAsAAAAwAAAwCZoAFAIAWhY2uSTL03AQEBAIA=");
            var ppsBytes = Convert.FromBase64String("RAHA8vA8kA==");
            var iFrameBytes = new byte[] { 0x0, 0x0, 0x0, 0x1, 0x26, 0x1, 0xa0, 0x9a, 0x87 };

            RawH265Frame frame = null;
            var parser = new H265Parser(() => DateTime.UtcNow) { FrameGenerated = rawFrame => frame = (RawH265Frame)rawFrame };
            parser.Parse(new ArraySegment<byte>(vpsBytes), false);
            parser.Parse(new ArraySegment<byte>(spsBytes), false);
            parser.Parse(new ArraySegment<byte>(ppsBytes), false);

            parser.ResetState();
            parser.Parse(new ArraySegment<byte>(iFrameBytes), true);

            Assert.IsInstanceOfType(frame, typeof(RawH265IFrame));
        }
    }
}
