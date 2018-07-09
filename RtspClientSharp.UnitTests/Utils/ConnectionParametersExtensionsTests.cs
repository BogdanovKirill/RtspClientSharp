using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Rtsp;
using RtspClientSharp.Utils;

namespace RtspClientSharp.UnitTests.Utils
{
    [TestClass]
    public class ConnectionParametersExtensionsTests
    {
        [TestMethod]
        public void GetFixedRtspUri_UriWithoutPort_ShouldSetDefaultRtspPort()
        {
            var uri = new Uri("rtsp://127.0.0.1");
            var connectionParameters = new ConnectionParameters(uri);

            var fixedUri = connectionParameters.GetFixedRtspUri();

            Assert.AreEqual(Constants.DefaultRtspPort, fixedUri.Port);
        }

        [TestMethod]
        public void GetFixedRtspUri_UriWithWrongScheme_ShouldSetRightScheme()
        {
            var uri = new Uri("http://127.0.0.1");
            var connectionParameters = new ConnectionParameters(uri);

            var fixedUri = connectionParameters.GetFixedRtspUri();

            Assert.AreEqual("rtsp", fixedUri.Scheme);
        }
    }
}