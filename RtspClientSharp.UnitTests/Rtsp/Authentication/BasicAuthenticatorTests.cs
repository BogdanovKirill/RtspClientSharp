using System;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Rtsp.Authentication;

namespace RtspClientSharp.UnitTests.Rtsp.Authentication
{
    [TestClass]
    public class BasicAuthenticatorTests
    {
        [TestMethod]
        public void GetResponse_TestArguments_NonEmptyResult()
        {
            var basicAuthenticator = new BasicAuthenticator(new NetworkCredential("1", "2"));

            string response = basicAuthenticator.GetResponse(1, "http://127.0.0.1", "GET", Array.Empty<byte>());

            Assert.IsNotNull(response);
            Assert.IsTrue(response.Length != 0);
        }
    }
}