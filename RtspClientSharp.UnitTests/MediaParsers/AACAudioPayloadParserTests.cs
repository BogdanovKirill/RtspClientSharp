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
    public class AACAudioPayloadParserTests
    {
        [TestMethod]
        public void Parse_TestData_ReturnsValidFrame()
        {
            var testCodecInfo = new AACCodecInfo
            {
                ConfigBytes = new byte[] {20, 8},
                IndexDeltaLength = 3,
                IndexLength = 3,
                SizeLength = 13
            };

            byte[] testBytes = Convert.FromBase64String(
                "ABAPyAE2Nay0eJ0OEMF4cOFyZeskqICVBWXLiAFDYHBG3GXFf0r/3nn+pRYcsdweO8Pzlxhjm0Q5QrEf7jx31b1PX8VuZOcZZ516jVY6sW/ISf8SQb6fONzpYJLiNRi7SO1o+" +
                "ByL8qHHiUeF9EoqyQ1VgamBApL8XaznlKoszkm9Td02961s0SznSmtSZcuhaEYCZaPe1k5qM+U3wHzs3CCnWMSNsK9UNCq1dzSm4YZWRkJPIY4oXJXsSCW67r8Xdb4slBDvxqu709z" +
                "ggyqrTuoOtkR3aHhFs6JHL8QwVgiusj39H8FrZ8Nu3Dt3qu0NQo5KpHN4R1jn2G71VHMfJwGEwZisU2e9Pz3h316dvpt97XbNcxLTgUCz9UpleUNXWAeygFCrPGTpWFv1Qn3JxS4W1xI" +
                "W5W/dHymYM5ElSMhtF+PFounb/IKbxKJThTUkxx3FWwYt00q7nMOilx2t6odQue/bTaENXKw304l1X5aiEl2S4+C0pIEzkHpbMp+5bmLw1+M8FNRkzlEngnPPATaJQSNOqTHTwVmK+" +
                "fAVSESminLM1TAOg0EZUw0pLTl3ka+scMp9kmxkFKLr3YRip0Uy0JMSnfefWxGezotf3sbV4afJZrnuVnKyanaK33korbN4LMIhvBgAAAAAAAAAAAAAAAAAAAAAAAAAA70=");

            byte[] frameBytes = Convert.FromBase64String(
                "D8gBNjWstHidDhDBeHDhcmXrJKiAlQVly4gBQ2BwRtxlxX9K/955/qUWHLHcHjvD85cYY5tEOUKxH+48d9W9T1/FbmTnGWedeo1" +
                "WOrFvyEn/EkG+nzjc6WCS4jUYu0jtaPgci/Khx4lHhfRKKskNVYGpgQKS/F2s55SqLM5JvU3dNvetbNEs50prUmXLoWhGAmWj3tZO" +
                "ajPlN8B87Nwgp1jEjbCvVDQqtXc0puGGVkZCTyGOKFyV7Egluu6/F3W+LJQQ78aru9Pc4IMqq07qDrZEd2h4RbOiRy/EMFYIrrI9/R/" +
                "Ba2fDbtw7d6rtDUKOSqRzeEdY59hu9VRzHycBhMGYrFNnvT894d9enb6bfe12zXMS04FAs/VKZXlDV1gHsoBQqzxk6Vhb9UJ9ycUuFt" +
                "cSFuVv3R8pmDORJUjIbRfjxaLp2/yCm8SiU4U1JMcdxVsGLdNKu5zDopcdreqHULnv202hDVysN9OJdV+WohJdkuPgtKSBM5B6WzKfuW" +
                "5i8NfjPBTUZM5RJ4JzzwE2iUEjTqkx08FZivnwFUhEpopyzNUwDoNBGVMNKS05d5GvrHDKfZJsZBSi692EYqdFMtCTEp33n1sRns6LX97" +
                "G1eGnyWa57lZysmp2it95KK2zeCzCIbwYAAAAAAAAAAAAAAAAAAAAAAAAAA==");

            RawAACFrame frame = null;
            var parser = new AACAudioPayloadParser(testCodecInfo);
            parser.FrameGenerated = rawFrame => frame = (RawAACFrame) rawFrame;
            parser.Parse(TimeSpan.Zero, new ArraySegment<byte>(testBytes), true);

            Assert.IsNotNull(frame);
            Assert.AreEqual(FrameType.Audio, frame.Type);
            Assert.IsTrue(frame.ConfigSegment.SequenceEqual(testCodecInfo.ConfigBytes));
            Assert.IsTrue(frame.FrameSegment.SequenceEqual(frameBytes));
        }
    }
}