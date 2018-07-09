using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Utils;

namespace RtspClientSharp.UnitTests.Utils
{
    [TestClass]
    public class BigEndianConverterTests
    {
        [TestMethod]
        public void ReadUInt32_TestBuffer_ReturnsCorrectInt()
        {
            uint expected = 0x11 << 24 | 0x22 << 16 | 0x33 << 8 | 0x44;
            var buffer = new byte[] {0x11, 0x22, 0x33, 0x44};

            uint result = BigEndianConverter.ReadUInt32(buffer, 0);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ReadUInt24_TestBuffer_ReturnsCorrectInt()
        {
            int expected = 0x22 << 16 | 0x33 << 8 | 0x44;
            var buffer = new byte[] {0x22, 0x33, 0x44};

            int result = BigEndianConverter.ReadUInt24(buffer, 0);

            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void ReadUInt16_TestBuffer_ReturnsCorrectInt()
        {
            int expected = 0x33 << 8 | 0x44;
            var buffer = new byte[] {0x33, 0x44};

            int result = BigEndianConverter.ReadUInt16(buffer, 0);

            Assert.AreEqual(expected, result);
        }
    }
}