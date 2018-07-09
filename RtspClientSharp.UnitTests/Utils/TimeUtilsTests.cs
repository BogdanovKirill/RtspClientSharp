using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Utils;

namespace RtspClientSharp.UnitTests.Utils
{
    [TestClass]
    public class TimeUtilsTests
    {
        [TestMethod]
        public void IsTimeOver_NotOverYet_ReturnsFalse()
        {
            int startTime = Environment.TickCount;

            Thread.Sleep(1000);
            bool result = TimeUtils.IsTimeOver(Environment.TickCount, startTime, 15 * 1000);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsTimeOver_TimeOver_ReturnsTrue()
        {
            int startTime = Environment.TickCount;

            Thread.Sleep(1000);
            bool result = TimeUtils.IsTimeOver(Environment.TickCount, startTime, 100);

            Assert.IsTrue(result);
        }
    }
}