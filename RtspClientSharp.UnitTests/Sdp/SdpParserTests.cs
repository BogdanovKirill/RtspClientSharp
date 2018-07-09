using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Codecs.Audio;
using RtspClientSharp.Codecs.Video;
using RtspClientSharp.RawFrames.Video;
using RtspClientSharp.Sdp;

namespace RtspClientSharp.UnitTests.Sdp
{
    [TestClass]
    public class SdpParserTests
    {
        [TestMethod]
        public void Parse_TestDocumentWithVideoAndAudioTracks_ReturnsTwoTracks()
        {
            string testInput = "m=video 0 RTP/AVP 96\r\n" +
                               "a=control:streamid=0\r\n" +
                               "a=range:npt=0-7.741000\r\n" +
                               "a=length:npt=7.741000\r\n" +
                               "a=rtpmap:96 H264/90000\r\n" +
                               "a=StreamName:string;\"hinted video track\"\r\n" +
                               "a=fmtp:96 packetization-mode=1; profile-level-id=420029; sprop-parameter-sets=Z0IAKeKQCgDLYC3AQEBpB4kRUA==,aM48gA==\r\n" +
                               "m=audio 0 RTP/AVP 97\r\n" +
                               "a=control:streamid=1\r\n" +
                               "a=range:npt=0-7.712000\r\n" +
                               "a=length:npt=7.712000\r\n" +
                               "a=rtpmap:97 mpeg4-generic/32000/2\r\n" +
                               "a=mimetype:string;\"audio/mpeg4-generic\"\r\n" +
                               "a=AvgBitRate:integer;65790\r\n" +
                               "a=StreamName:string;\"hinted audio track\"\r\n";

            var testBytes = Encoding.ASCII.GetBytes(testInput);

            var parser = new SdpParser();
            IReadOnlyList<RtspMediaTrackInfo> tracks = parser.Parse(testBytes).Where(t => t is RtspMediaTrackInfo)
                .Cast<RtspMediaTrackInfo>().ToList();

            Assert.AreEqual(2, tracks.Count);
            RtspMediaTrackInfo videoTrack = tracks.First(t => t.Codec is VideoCodecInfo);
            Assert.AreEqual("streamid=0", videoTrack.TrackName);
            Assert.AreEqual(90000, videoTrack.SamplesFrequency);
            RtspMediaTrackInfo audioTrack = tracks.First(t => t.Codec is AudioCodecInfo);
            Assert.AreEqual("streamid=1", audioTrack.TrackName);
            Assert.AreEqual(32000, audioTrack.SamplesFrequency);
        }

        [TestMethod]
        public void Parse_SDPWithH264Track_ReturnsWithH264CodecTrack()
        {
            IEnumerable<byte> spsBytes =
                RawH264Frame.StartMarker.Concat(Convert.FromBase64String("Z2QAFKzZQ0R+f/zBfMMAQAAAAwBAAAAKI8UKZYA="));
            IEnumerable<byte> ppsBytes = RawH264Frame.StartMarker.Concat(Convert.FromBase64String("aOvssiw="));

            string testInput = "m=video 0 RTP/AVP 96\r\n" +
                               "a=control:streamid=0\r\n" +
                               "a=rtpmap:96 H264/90000\r\n" +
                               "a=fmtp:96 profile-level-id=640014;sprop-parameter-sets=Z2QAFKzZQ0R+f/zBfMMAQAAAAwBAAAAKI8UKZYA=,aOvssiw=\r\n";

            var testBytes = Encoding.ASCII.GetBytes(testInput);

            var parser = new SdpParser();
            RtspMediaTrackInfo videoTrack = parser.Parse(testBytes).Where(t => t is RtspMediaTrackInfo)
                .Cast<RtspMediaTrackInfo>().First();

            H264CodecInfo codecInfo = (H264CodecInfo) videoTrack.Codec;
            Assert.IsTrue(spsBytes.Concat(ppsBytes).SequenceEqual(codecInfo.SpsPpsBytes));
        }

        [TestMethod]
        public void Parse_SDPWithAACTrack_ReturnsWithAACCodecTrack()
        {
            var configBytes = new byte[] {0x12, 0x10};
            string testInput = "m=audio 0 RTP/AVP 96\r\n" +
                               "b=AS:128\r\n" +
                               "a=rtpmap:96 MPEG4-GENERIC/44100/2\r\n" +
                               "a=fmtp:96 profile-level-id=1;mode=AAC-hbr;sizelength=13;indexlength=3;indexdeltalength=3; config=1210\r\n" +
                               "a=control:streamid=0\r\n";

            var testBytes = Encoding.ASCII.GetBytes(testInput);

            var parser = new SdpParser();
            RtspMediaTrackInfo audioTrack = parser.Parse(testBytes).Where(t => t is RtspMediaTrackInfo)
                .Cast<RtspMediaTrackInfo>().First();

            AACCodecInfo codecInfo = (AACCodecInfo) audioTrack.Codec;
            Assert.AreEqual(3, codecInfo.IndexDeltaLength);
            Assert.AreEqual(3, codecInfo.IndexLength);
            Assert.AreEqual(13, codecInfo.SizeLength);
            Assert.IsTrue(configBytes.SequenceEqual(codecInfo.ConfigBytes));
        }

        [TestMethod]
        public void Parse_SDPWithJPEGTrack_ReturnsWithJPEGCodecTrack()
        {
            string testInput = "m=video 0 RTP/AVP 26\r\n" +
                               "c=IN IP4 0.0.0.0\r\n" +
                               "a=control:track1\r\n";

            var testBytes = Encoding.ASCII.GetBytes(testInput);

            var parser = new SdpParser();
            RtspMediaTrackInfo videoTrack = parser.Parse(testBytes).Where(t => t is RtspMediaTrackInfo)
                .Cast<RtspMediaTrackInfo>().First();

            Assert.IsInstanceOfType(videoTrack.Codec, typeof(MJPEGCodecInfo));
        }

        [TestMethod]
        public void Parse_SDPWithG711UTrack_ReturnsWithG711UCodecTrack()
        {
            string testInput = "m=audio 0 RTP/AVP 0\r\n" +
                               "a=rtpmap:0 PCMU/16000/2\r\n" +
                               "a=control:trackID=2\r\n";

            var testBytes = Encoding.ASCII.GetBytes(testInput);

            var parser = new SdpParser();
            RtspMediaTrackInfo audioTrack = parser.Parse(testBytes).Where(t => t is RtspMediaTrackInfo)
                .Cast<RtspMediaTrackInfo>().First();

            G711UCodecInfo codecInfo = (G711UCodecInfo) audioTrack.Codec;
            Assert.AreEqual(2, codecInfo.Channels);
            Assert.AreEqual(16000, codecInfo.SampleRate);
        }

        [TestMethod]
        [DataRow(16)]
        [DataRow(24)]
        [DataRow(32)]
        [DataRow(40)]
        public void Parse_SDPWithG726Track_ReturnsWithG726CodecTrack(int bitrate)
        {
            string testInput = "m=audio 0 RTP/AVP 97\r\n" +
                               "a=control:trackID=1\r\n" +
                               $"a=rtpmap:97 G726-{bitrate}/8000\r\n";

            var testBytes = Encoding.ASCII.GetBytes(testInput);

            var parser = new SdpParser();
            RtspMediaTrackInfo audioTrack = parser.Parse(testBytes).Where(t => t is RtspMediaTrackInfo)
                .Cast<RtspMediaTrackInfo>().First();

            G726CodecInfo codecInfo = (G726CodecInfo) audioTrack.Codec;
            Assert.AreEqual(1, codecInfo.Channels);
            Assert.AreEqual(8000, codecInfo.SampleRate);
            Assert.AreEqual(bitrate * 1000, codecInfo.Bitrate);
        }

        [TestMethod]
        [DataRow(16)]
        [DataRow(8)]
        public void Parse_SDPWithPCMTrack_ReturnsWithPCMCodecTrack(int bitsPerSample)
        {
            string testInput = "m=audio 0 RTP/AVP 97\r\n" +
                               "c=IN IP4 0.0.0.0\r\n" +
                               "b=AS:0\r\n" +
                               $"a=rtpmap:97 L{bitsPerSample}/16000\r\n" +
                               "a=control:track2\r\n";

            var testBytes = Encoding.ASCII.GetBytes(testInput);

            var parser = new SdpParser();
            RtspMediaTrackInfo audioTrack = parser.Parse(testBytes).Where(t => t is RtspMediaTrackInfo)
                .Cast<RtspMediaTrackInfo>().First();

            PCMCodecInfo codecInfo = (PCMCodecInfo) audioTrack.Codec;
            Assert.AreEqual(1, codecInfo.Channels);
            Assert.AreEqual(16000, codecInfo.SampleRate);
            Assert.AreEqual(bitsPerSample, codecInfo.BitsPerSample);
        }
    }
}