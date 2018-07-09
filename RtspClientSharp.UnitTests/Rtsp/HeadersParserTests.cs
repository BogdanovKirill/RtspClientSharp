using System.Collections.Specialized;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Rtsp;

namespace RtspClientSharp.UnitTests.Rtsp
{
    [TestClass]
    public class HeadersParserTests
    {
        [TestMethod]
        public void ParseHeaders_TestData_ReturnsValidHeaders()
        {
            string testInput = "HEADER1:VALUE1\r\n" +
                               "header2 : VALUE2\r\n" +
                               "hEADER3: VALUE3\r\n" +
                               "badHeader\r\n";

            byte[] testBytes = Encoding.ASCII.GetBytes(testInput);
            var ms = new MemoryStream(testBytes);
            var streamReader = new StreamReader(ms, Encoding.ASCII);

            NameValueCollection headers = HeadersParser.ParseHeaders(streamReader);

            Assert.AreEqual(3, headers.Count);
            Assert.AreEqual("VALUE1", headers["HEADER1"]);
            Assert.AreEqual("VALUE2", headers["HEADER2"]);
            Assert.AreEqual("VALUE3", headers["HEADER3"]);
        }
    }
}