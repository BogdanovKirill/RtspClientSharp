using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Utils;

namespace RtspClientSharp.UnitTests.Utils
{
    [TestClass]
    public class HexTests
    {
        [TestMethod]
        public void StringToByteArray_EmptyString_ReturnsEmptyArray()
        {
            byte[] resultBytes = Hex.StringToByteArray(string.Empty);

            Assert.AreEqual(0, resultBytes.Length);
        }

        [TestMethod]
        public void StringToByteArray_TestString_ValidResult()
        {
            var expectedBytes = new byte[] {0xFF, 0xAA, 0xBB, 0xCC};

            byte[] resultBytes = Hex.StringToByteArray("FFAABBCC");

            Assert.IsTrue(expectedBytes.SequenceEqual(resultBytes));
        }
    }
}