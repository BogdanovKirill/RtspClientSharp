using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Utils;

namespace RtspClientSharp.UnitTests.Utils
{
    [TestClass]
    public class NetworkCredentialExtensionsTests
    {
        [TestMethod]
        public void IsEmpty_EmptyUserName_ReturnsTrue()
        {
            var credentials = new NetworkCredential(string.Empty, "123123");

            bool result = credentials.IsEmpty();

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsEmpty_EmptyUserPassword_ReturnsFalse()
        {
            var credentials = new NetworkCredential("12314", string.Empty);

            bool result = credentials.IsEmpty();

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsEmpty_UserNameAndPasswordSet_ReturnsFalse()
        {
            var credentials = new NetworkCredential("user", "pass");

            bool result = credentials.IsEmpty();

            Assert.IsFalse(result);
        }
    }
}