using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Codecs.Audio;
using RtspClientSharp.MediaParsers;
using RtspClientSharp.RawFrames;
using RtspClientSharp.RawFrames.Audio;

namespace RtspClientSharp.UnitTests.MediaParsers
{
    [TestClass]
    public class G726AudioPayloadParserTests
    {
        [TestMethod]
        [DataRow(16000)]
        [DataRow(24000)]
        [DataRow(32000)]
        [DataRow(40000)]
        public void Parse_TestData_ReturnsValidFrame(int bitrate)
        {
            var testCodecInfo = new G726CodecInfo(bitrate);

            byte[] testBytes = {1, 2, 3, 4, 5, 6, 7, 8};

            RawG726Frame frame = null;
            var parser = new G726AudioPayloadParser(testCodecInfo);
            parser.FrameGenerated = rawFrame => frame = (RawG726Frame) rawFrame;
            parser.Parse(TimeSpan.Zero, new ArraySegment<byte>(testBytes), true);

            Assert.IsNotNull(frame);
            Assert.AreEqual(FrameType.Audio, frame.Type);
            Assert.AreEqual(bitrate / 8000, frame.BitsPerCodedSample);
            Assert.IsTrue(frame.FrameSegment.SequenceEqual(testBytes));
        }
    }
}