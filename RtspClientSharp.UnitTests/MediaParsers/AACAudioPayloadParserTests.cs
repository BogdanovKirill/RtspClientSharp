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
                "ATY1rLR4nQ4QwXhw4XJl6ySogJUFZcuIAUNgcEbcZcV/Sv/eef6lFhyx3B47w/OXGGObRDlCsR/uPHfVvU9fxW5k5xlnnXqNVjqxb8hJ/xJBvp843OlgkuI1GLtI7Wj4HIvyoce" +
                "JR4X0SirJDVWBqYECkvxdrOeUqizOSb1N3Tb3rWzRLOdKa1Jly6FoRgJlo97WTmoz5TfAfOzcIKdYxI2wr1Q0KrV3NKbhhlZGQk8hjihclexIJbruvxd1viyUEO/Gq7vT3OCDKqtO6" +
                "g62RHdoeEWzokcvxDBWCK6yPf0fwWtnw27cO3eq7Q1Cjkqkc3hHWOfYbvVUcx8nAYTBmKxTZ70/PeHfXp2+m33tds1zEtOBQLP1SmV5Q1dYB7KAUKs8ZOlYW/VCfcnFLhbXEhblb90f" +
                "KZgzkSVIyG0X48Wi6dv8gpvEolOFNSTHHcVbBi3TSrucw6KXHa3qh1C579tNoQ1crDfTiXVflqISXZLj4LSkgTOQelsyn7luYvDX4zwU1GTOUSeCc88BNolBI06pMdPBWYr58BVIRKaK" +
                "cszVMA6DQRlTDSktOXeRr6xwyn2SbGQUouvdhGKnRTLQkxKd959bEZ7Oi1/extXhp8lmue5WcrJqdorfeSits3gswiG8GAAAAAAAAAAAAAAAAAAAAAAAAAADvQ==");

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