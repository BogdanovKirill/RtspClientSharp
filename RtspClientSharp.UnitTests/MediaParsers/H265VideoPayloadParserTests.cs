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
        [DataRow(new byte[] { 0x24, 0x00, 0x05, 0xAC, 0x80, 0x62, 0x91, 0x16, 0x00, 0x00, 0x91, 0x12, 0x36, 0x63, 0x91 }, DisplayName = "FP")]
        //[DataRow(new byte[] { }, DisplayName = "AP")]
        public void Parse_DifferentUnits_ReturnsValidIFrame(byte[] testBytes)
        {
            H265CodecInfo testCodecInfo = CreateTestH265CodecInfo();

            RawH265Frame frame = null;
            var parser = new H265VideoPayloadParser(testCodecInfo);
            parser.FrameGenerated = rawFrame => frame = (RawH265Frame)rawFrame;
            parser.Parse(TimeSpan.Zero, new ArraySegment<byte>(testBytes), true);

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
