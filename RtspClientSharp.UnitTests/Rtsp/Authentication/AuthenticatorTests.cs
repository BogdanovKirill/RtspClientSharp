using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Rtsp.Authentication;

namespace RtspClientSharp.UnitTests.Rtsp.Authentication
{
    [TestClass]
    public class AuthenticatorTests
    {
        private readonly NetworkCredential _testCredentials = new NetworkCredential("1", "2");

        [TestMethod]
        public void Create_BasicAuth_ReturnsBasicAuthenticator()
        {
            var authenticator = Authenticator.Create(_testCredentials, "Basic realm=\"testRealm\"");

            Assert.IsInstanceOfType(authenticator, typeof(BasicAuthenticator));
        }

        [TestMethod]
        public void Create_DigestAuth_ReturnsDigestAuthenticator()
        {
            var authenticator = Authenticator.Create(_testCredentials,
                "Digest realm=\"testRealm\", nonce=\"0043f1fbY0638268723087242107798f0af052abf88c3f\", stale=FALSE, qop=\"auth\"");

            Assert.IsInstanceOfType(authenticator, typeof(DigestAuthenticator));
        }
    }
}