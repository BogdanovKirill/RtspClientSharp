using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Codecs.Video;
using RtspClientSharp.MediaParsers;
using RtspClientSharp.RawFrames.Video;

namespace RtspClientSharp.UnitTests.MediaParsers
{
    [TestClass]
    public class H264VideoPayloadParserTests
    {
        [TestMethod]
        [DataRow(new byte[] {0x7C, 0xC5, 0x88, 0x80, 0x10, 0x00}, DisplayName = "FUA")]
        [DataRow(new byte[] {0x7D, 0xC5, 0x00, 0x00, 0x88, 0x80, 0x10, 0x00}, DisplayName = "FUB")]
        [DataRow(new byte[] {0x18, 0x00, 0x05, 0x65, 0x88, 0x80, 0x10, 0x00}, DisplayName = "STAPA")]
        [DataRow(new byte[] {0x19, 0x00, 0x00, 0x00, 0x05, 0x65, 0x88, 0x80, 0x10, 0x00}, DisplayName = "STAB")]
        [DataRow(new byte[] {0x1A, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x65, 0x88, 0x80, 0x10, 0x00},
            DisplayName = "MTAP16")]
        [DataRow(new byte[] {0x1B, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x65, 0x88, 0x80, 0x10, 0x00},
            DisplayName = "MTAP24")]
        public void Parse_DifferentAggregationUnits_ReturnsValidIFrame(byte[] testBytes)
        {
            H264CodecInfo testCodecInfo = CreateTestH264CodecInfo();

            RawH264Frame frame = null;
            var parser = new H264VideoPayloadParser(testCodecInfo);
            parser.FrameGenerated = rawFrame => frame = (RawH264Frame) rawFrame;
            parser.Parse(TimeSpan.Zero, new ArraySegment<byte>(testBytes), true);

            Assert.IsNotNull(frame);
            Assert.IsInstanceOfType(frame, typeof(RawH264IFrame));
        }

        [TestMethod]
        public void Constructor_EmptySpsPps_NoExceptionGenerated()
        {
            H264CodecInfo testCodecInfo = new H264CodecInfo {SpsPpsBytes = Array.Empty<byte>()};

            new H264VideoPayloadParser(testCodecInfo);
        }

        private static H264CodecInfo CreateTestH264CodecInfo()
        {
            var testCodecInfo = new H264CodecInfo();

            var spsBytes = Convert.FromBase64String("AAAAAWdNQCmaZgUB7YC1AQEBBenA");
            var ppsBytes = Convert.FromBase64String("AAAAAWjuPIA=");

            testCodecInfo.SpsPpsBytes = spsBytes.Concat(ppsBytes).ToArray();
            return testCodecInfo;
        }
    }
}