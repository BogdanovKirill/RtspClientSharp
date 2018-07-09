using System;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Rtsp.Authentication;

namespace RtspClientSharp.UnitTests.Rtsp.Authentication
{
    [TestClass]
    public class DigestAuthenticatorTests
    {
        [TestMethod]
        [DataRow("", DisplayName = "without qop")]
        [DataRow("auth", DisplayName = "qop = auth")]
        [DataRow("auth-int", DisplayName = "qop = auth-int")]
        public void GetResponse_TestArguments_NonEmptyResult(string qop)
        {
            var digestAuthenticator =
                new DigestAuthenticator(new NetworkCredential("1", "2"), "testRealm", "1234", qop);

            string response = digestAuthenticator.GetResponse(1, "http://127.0.0.1", "GET", Array.Empty<byte>());

            Assert.IsNotNull(response);
            Assert.IsTrue(response.Length != 0);
        }
    }
}