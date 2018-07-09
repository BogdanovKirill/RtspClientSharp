using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Rtsp.Authentication;

namespace RtspClientSharp.UnitTests.Rtsp.Authentication
{
    [TestClass]
    public class MD5Tests
    {
        [TestMethod]
        public void GetHashHexValues_EmptyString_ReturnsValidHash()
        {
            string hash = MD5.GetHashHexValues("");
            Assert.AreEqual("d41d8cd98f00b204e9800998ecf8427e", hash);
        }
    }
}