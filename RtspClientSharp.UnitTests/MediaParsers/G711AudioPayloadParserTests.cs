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
    public class G711AudioPayloadParserTests
    {
        [TestMethod]
        public void Parse_TestData_ReturnsValidFrame()
        {
            var testCodecInfo = new G711UCodecInfo();

            byte[] testBytes = {1, 2, 3, 4, 5, 6, 7, 8};

            RawG711UFrame frame = null;
            var parser = new G711AudioPayloadParser(testCodecInfo);
            parser.FrameGenerated = rawFrame => frame = (RawG711UFrame) rawFrame;
            parser.Parse(TimeSpan.Zero, new ArraySegment<byte>(testBytes), true);

            Assert.IsNotNull(frame);
            Assert.AreEqual(FrameType.Audio, frame.Type);
            Assert.IsTrue(frame.FrameSegment.SequenceEqual(testBytes));
        }
    }
}