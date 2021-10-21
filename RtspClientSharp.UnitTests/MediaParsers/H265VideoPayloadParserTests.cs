using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Codecs.Video;
using RtspClientSharp.MediaParsers;
using RtspClientSharp.RawFrames.Video;
using System;
using System.Linq;

namespace RtspClientSharp.UnitTests.MediaParsers
{
    [TestClass]
    public class H265VideoPayloadParserTests
    {
        [TestMethod]
        public void Parse_FragmentationUnit_ReturnsValidIFrame()
        {
            H265CodecInfo testCodecInfo = CreateTestH265CodecInfo();

            var testBytesStartMark = new byte[] { 0x62, 0x01, 0x93, 0xAF, 0x8A, 0xB4, 0xB1, 0x0A, 0x80, 0xF3, 0xE3, 0x76, 0x74, 0xC2, 0xA3 };
            var testBytesEndMark = new byte[] { 0x62, 0x01, 0x53, 0xF7, 0x7E, 0x59, 0xE1, 0x4D, 0x79, 0xBA, 0xF3, 0xDF, 0x4F, 0xE3 };

            RawH265Frame frame = null;
            var parser = new H265VideoPayloadParser(testCodecInfo);
            parser.FrameGenerated = rawFrame => frame = (RawH265Frame)rawFrame;
            parser.Parse(TimeSpan.Zero, new ArraySegment<byte>(testBytesStartMark), true);
            parser.Parse(TimeSpan.Zero, new ArraySegment<byte>(testBytesEndMark), true);

            Assert.IsNotNull(frame);
            Assert.IsInstanceOfType(frame, typeof(RawH265IFrame));
        }

        [TestMethod]
        public void Parse_AggregationUnit_ReturnsValidIFrame()
        {
            H265CodecInfo testCodecInfo = CreateTestH265CodecInfo();

            var testBytesFirstFrame = new byte[] { 0x61, 0x01, 0x93, 0xAF, 0x8A, 0xB4, 0xB1, 0x0A, 0x80, 0xF3, 0xE3, 0x76, 0x74, 0xC2, 0xA3 };
            var testBytesSecondFrame = new byte[] { 0x61, 0x01, 0x53, 0xF7, 0x7E, 0x59, 0xE1, 0x4D, 0x79, 0xBA, 0xF3, 0xDF, 0x4F, 0xE3 };

            RawH265Frame frame = null;
            var parser = new H265VideoPayloadParser(testCodecInfo);
            parser.FrameGenerated = rawFrame => frame = (RawH265Frame)rawFrame;
            parser.Parse(TimeSpan.Zero, new ArraySegment<byte>(testBytesFirstFrame), true);
            parser.Parse(TimeSpan.Zero, new ArraySegment<byte>(testBytesSecondFrame), true);

            Assert.IsNotNull(frame);
            Assert.IsInstanceOfType(frame, typeof(RawH265IFrame));
        }

        [TestMethod]
        public void Constructor_EmptyVpsSpsPps_NotExceptionGenerated()
        {
            H265CodecInfo testCodecInfo = new H265CodecInfo { VpsBytes = Array.Empty<byte>(), SpsBytes = Array.Empty<byte>(), PpsBytes = Array.Empty<byte>() };

            new H265VideoPayloadParser(testCodecInfo);
        }

        private static H265CodecInfo CreateTestH265CodecInfo()
        {
            var testCodecInfo = new H265CodecInfo();

            var vpsBytes = Convert.FromBase64String("QAEMAf//AWAAAAMAsAAAAwAAAwCZrAk=");
            var spsBytes = Convert.FromBase64String("QgEBAWAAAAMAsAAAAwAAAwCZoAFAIAWhY2uSTL03AQEBAIA=");
            var ppsBytes = Convert.FromBase64String("RAHA8vA8kA==");

            testCodecInfo.VpsBytes = vpsBytes.ToArray();
            testCodecInfo.SpsBytes = spsBytes.ToArray();
            testCodecInfo.PpsBytes = ppsBytes.ToArray();
            return testCodecInfo;
        }
    }
}
