using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Utils;

namespace RtspClientSharp.UnitTests.Utils
{
    [TestClass]
    public class ArrayUtilsTests
    {
        [TestMethod]
        public void IsBytesEquals_EqualArrays_ReturnsTrue()
        {
            var bytes1 = new byte[] {1, 2, 3, 4};
            var bytes2 = new byte[] {0, 2, 1, 2, 3, 4, 5};

            bool result = ArrayUtils.IsBytesEquals(bytes1, 0, bytes1.Length, bytes2, 2, bytes1.Length);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsBytesEquals_DifferentLengths_ReturnsFalse()
        {
            var bytes1 = new byte[] {1};
            var bytes2 = new byte[] {0, 2, 1, 2, 3, 4, 5};

            bool result = ArrayUtils.IsBytesEquals(bytes1, 0, bytes1.Length, bytes2, 2, bytes2.Length);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsBytesEquals_DifferentBodies_ReturnsFalse()
        {
            var bytes1 = new byte[] {1, 3, 4, 5};
            var bytes2 = new byte[] {0, 2, 1, 2, 3, 4, 5};

            bool result = ArrayUtils.IsBytesEquals(bytes1, 0, bytes1.Length, bytes2, 2, bytes2.Length);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void StartsWith_PatternExists_ReturnsTrue()
        {
            var pattern = new Byte[] {1, 2, 3};
            var bytes = new byte[] {0, 1, 2, 3, 4, 5, 6, 7};

            bool result = ArrayUtils.StartsWith(bytes, 1, bytes.Length - 1, pattern);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void StartsWith_PatternNotExists_ReturnsFalse()
        {
            var pattern = new Byte[] {1, 2, 3, 9};
            var bytes = new byte[] {0, 1, 2, 3, 4, 5, 6, 7};

            bool result = ArrayUtils.StartsWith(bytes, 1, bytes.Length - 1, pattern);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void EndsWith_PatternExists_ReturnsTrue()
        {
            var pattern = new Byte[] {1, 2, 3};
            var bytes = new byte[] {0, 5, 6, 7, 1, 2, 3};

            bool result = ArrayUtils.EndsWith(bytes, 1, bytes.Length - 1, pattern);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void EndsWith_PatternNotExists_ReturnsFalse()
        {
            var pattern = new Byte[] {1, 2, 3, 9};
            var bytes = new byte[] {0, 5, 6, 7, 1, 2, 3};

            bool result = ArrayUtils.EndsWith(bytes, 1, bytes.Length - 1, pattern);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IndexOfBytes_PatternExists_ReturnsActualIndex()
        {
            var pattern = new Byte[] {1, 2, 3};
            var bytes = new byte[] {0, 5, 6, 7, 1, 2, 3};

            int index = ArrayUtils.IndexOfBytes(bytes, pattern, 1, bytes.Length - 1);

            Assert.AreEqual(4, index);
        }

        [TestMethod]
        public void IndexOfBytes_PatternNotExists_ReturnsMinusOne()
        {
            var pattern = new Byte[] {1, 2, 3};
            var bytes = new byte[] {0, 5, 6, 7, 1, 2};

            int index = ArrayUtils.IndexOfBytes(bytes, pattern, 1, bytes.Length - 1);

            Assert.AreEqual(-1, index);
        }

        [TestMethod]
        public void LastIndexOfBytes_PatternExists_ReturnsActualIndex()
        {
            var pattern = new Byte[] { 1, 2, 3 };
            var bytes = new byte[] { 0, 5, 6, 7, 1, 2, 3 };

            int index = ArrayUtils.LastIndexOfBytes(bytes, pattern, 1, bytes.Length - 1);

            Assert.AreEqual(4, index);
        }

        [TestMethod]
        public void LastIndexOfBytes_PatternNotExists_ReturnsMinusOne()
        {
            var pattern = new Byte[] { 1, 2, 3 };
            var bytes = new byte[] { 0, 5, 6, 7, 1, 2 };

            int index = ArrayUtils.LastIndexOfBytes(bytes, pattern, 1, bytes.Length - 1);

            Assert.AreEqual(-1, index);
        }
    }
}