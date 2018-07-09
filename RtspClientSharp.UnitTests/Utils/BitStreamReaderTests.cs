using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Utils;

namespace RtspClientSharp.UnitTests.Utils
{
    [TestClass]
    public class BitStreamReaderTests
    {
        [TestMethod]
        public void ReadBit_ReadSeveralBits_ValidResult()
        {
            var bytes = new byte[] {0x31};
            var reader = new BitStreamReader();
            reader.ReInitialize(new ArraySegment<byte>(bytes));

            int bit1 = reader.ReadBit();
            int bit2 = reader.ReadBit();
            int bit3 = reader.ReadBit();
            int bit4 = reader.ReadBit();
            int bit5 = reader.ReadBit();

            Assert.AreEqual(0, bit1);
            Assert.AreEqual(0, bit2);
            Assert.AreEqual(1, bit3);
            Assert.AreEqual(1, bit4);
            Assert.AreEqual(0, bit5);
        }

        [TestMethod]
        public void ReadBits_ReadSeveralBits_ValidResult()
        {
            var bytes = new byte[] {0x31};
            var reader = new BitStreamReader();
            reader.ReInitialize(new ArraySegment<byte>(bytes));

            int bits = reader.ReadBits(5);

            Assert.AreEqual(6, bits);
        }

        [TestMethod]
        public void ReadUe_TestCode_ValidResult()
        {
            const int testCode = 4;
            var bytes = new byte[] {(testCode + 1) << 3};
            var reader = new BitStreamReader();
            reader.ReInitialize(new ArraySegment<byte>(bytes));

            int code = reader.ReadUe();

            Assert.AreEqual(testCode, code);
        }
    }
}