using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.MediaParsers;
using RtspClientSharp.RawFrames;
using RtspClientSharp.RawFrames.Video;

namespace RtspClientSharp.UnitTests.MediaParsers
{
    [TestClass]
    public class MJPEGVideoPayloadParserTests
    {
        [TestMethod]
        public void Parse_TestData_ReturnsValidFrame()
        {
            byte[] header = GenerateFakeHeader();
            byte[] testBytes1 = header.Concat(new byte[] {1, 2, 3, 4, 5, 6, 7, 8}).ToArray();
            byte[] testBytes2 = header.Concat(new byte[] {1, 2, 3, 4}).ToArray();

            RawJpegFrame frame = null;
            var parser = new MJPEGVideoPayloadParser();
            parser.FrameGenerated = rawFrame => frame = (RawJpegFrame) rawFrame;
            parser.Parse(TimeSpan.Zero, new ArraySegment<byte>(testBytes1), false);
            parser.Parse(TimeSpan.Zero, new ArraySegment<byte>(testBytes2), false);

            Assert.IsNotNull(frame);
            Assert.AreEqual(FrameType.Video, frame.Type);
            Assert.IsTrue(frame.FrameSegment.Count > 0);
        }

        private byte[] GenerateFakeHeader()
        {
            int offset = 0;

            var header = new byte[8];
            header[offset++] = 0; //type-specific
            header[offset++] = 0; //fragment offset hi
            header[offset++] = 0; //fragment offset mid
            header[offset++] = 0; //fragment offset low
            header[offset++] = 1; //type
            header[offset++] = 1; //q
            header[offset++] = 640 / 8; //width / 8
            header[offset] = 480 / 8; //height / 8

            return header;
        }
    }
}