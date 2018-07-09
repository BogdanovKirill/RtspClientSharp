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
    public class PCMAudioPayloadParserTests
    {
        [TestMethod]
        public void Parse_TestData_ReturnsValidFrame()
        {
            var testCodecInfo = new PCMCodecInfo(44100, 16, 2);

            byte[] testBytes = {1, 2, 3, 4, 5, 6, 7, 8};

            RawPCMFrame frame = null;
            var parser = new PCMAudioPayloadParser(testCodecInfo);
            parser.FrameGenerated = rawFrame => frame = (RawPCMFrame) rawFrame;
            parser.Parse(TimeSpan.Zero, new ArraySegment<byte>(testBytes), true);

            Assert.IsNotNull(frame);
            Assert.AreEqual(FrameType.Audio, frame.Type);
            Assert.IsTrue(frame.FrameSegment.SequenceEqual(testBytes));
            Assert.AreEqual(44100, frame.SampleRate);
            Assert.AreEqual(16, frame.BitsPerSample);
            Assert.AreEqual(2, frame.Channels);
        }
    }
}