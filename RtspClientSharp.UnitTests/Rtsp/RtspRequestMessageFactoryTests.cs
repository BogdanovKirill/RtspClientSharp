using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Rtsp;

namespace RtspClientSharp.UnitTests.Rtsp
{
    [TestClass]
    public class RtspRequestMessageFactoryTests
    {
        private static readonly Uri FakeUri = new Uri("rtsp://127.0.0.1");
        private const string UserAgent = "TestAgent";

        [TestMethod]
        public void EnsureCSeqIncreasesAfterEveryCreatedRequest()
        {
            var factory = new RtspRequestMessageFactory(FakeUri, null);

            RtspRequestMessage request1 = factory.CreateOptionsRequest();
            RtspRequestMessage request2 = factory.CreateDescribeRequest();

            Assert.AreEqual(1u, request1.CSeq);
            Assert.AreEqual(2u, request2.CSeq);
        }

        [TestMethod]
        public void CreateOptionsRequest_ValidProperties()
        {
            var factory = new RtspRequestMessageFactory(FakeUri, UserAgent);

            RtspRequestMessage request = factory.CreateOptionsRequest();

            Assert.AreEqual(RtspMethod.OPTIONS, request.Method);
            Assert.AreEqual(FakeUri, request.ConnectionUri);
            Assert.AreEqual(UserAgent, request.UserAgent);
        }

        [TestMethod]
        public void CreateDescribeRequest_ValidProperties()
        {
            var factory = new RtspRequestMessageFactory(FakeUri, UserAgent);

            RtspRequestMessage request = factory.CreateDescribeRequest();

            Assert.AreEqual(RtspMethod.DESCRIBE, request.Method);
            Assert.AreEqual(FakeUri, request.ConnectionUri);
            Assert.AreEqual(UserAgent, request.UserAgent);
        }

        [TestMethod]
        public void CreateSetupTcpInterleavedRequest_ValidProperties()
        {
            const string testTrackName = "testTrack";
            var factory = new RtspRequestMessageFactory(FakeUri, UserAgent);

            RtspRequestMessage request = factory.CreateSetupTcpInterleavedRequest(testTrackName, 1, 2);

            Assert.AreEqual(RtspMethod.SETUP, request.Method);
            Assert.AreEqual(FakeUri + "testTrack", request.ConnectionUri.ToString());
            Assert.AreEqual(UserAgent, request.UserAgent);
            string transportHeaderValue = request.Headers.Get("Transport");
            Assert.IsTrue(transportHeaderValue.Contains("TCP"));
            Assert.IsTrue(transportHeaderValue.Contains($"{1}-{2}"));
        }

        [TestMethod]
        public void CreateSetupUdpUnicastRequest_ValidProperties()
        {
            const string testTrackName = "testTrack";
            var factory = new RtspRequestMessageFactory(FakeUri, UserAgent);

            RtspRequestMessage request = factory.CreateSetupUdpUnicastRequest(testTrackName, 1, 2);

            Assert.AreEqual(RtspMethod.SETUP, request.Method);
            Assert.AreEqual(FakeUri + "testTrack", request.ConnectionUri.ToString());
            Assert.AreEqual(UserAgent, request.UserAgent);
            string transportHeaderValue = request.Headers.Get("Transport");
            Assert.IsTrue(transportHeaderValue.Contains("unicast"));
            Assert.IsTrue(transportHeaderValue.Contains($"{1}-{2}"));
        }

        [TestMethod]
        public void CreateGetParameterRequest_ValidProperties()
        {
            var factory = new RtspRequestMessageFactory(FakeUri, UserAgent);

            RtspRequestMessage request = factory.CreateGetParameterRequest();

            Assert.AreEqual(RtspMethod.GET_PARAMETER, request.Method);
            Assert.AreEqual(FakeUri, request.ConnectionUri);
            Assert.AreEqual(UserAgent, request.UserAgent);
        }

        [TestMethod]
        public void CreatePlayRequest_ValidProperties()
        {
            var factory = new RtspRequestMessageFactory(FakeUri, UserAgent);

            RtspRequestMessage request = factory.CreatePlayRequest();

            Assert.AreEqual(RtspMethod.PLAY, request.Method);
            Assert.AreEqual(FakeUri, request.ConnectionUri);
            Assert.AreEqual(UserAgent, request.UserAgent);
        }


        [TestMethod]
        public void CreatePlayRequest_ContentBaseAndSessionAreSet_ValidProperties()
        {
            var factory = new RtspRequestMessageFactory(FakeUri, UserAgent)
            {
                SessionId = "testSessionId",
                ContentBase = new Uri("http://127.0.0.1/base")
            };

            RtspRequestMessage request = factory.CreatePlayRequest();

            Assert.AreEqual(factory.SessionId, request.Headers.Get("Session"));
            Assert.AreEqual(factory.ContentBase, request.ConnectionUri);
        }

        [TestMethod]
        public void CreateTeardownRequest_ValidProperties()
        {
            var factory = new RtspRequestMessageFactory(FakeUri, UserAgent);

            RtspRequestMessage request = factory.CreateTeardownRequest();

            Assert.AreEqual(RtspMethod.TEARDOWN, request.Method);
            Assert.AreEqual(FakeUri, request.ConnectionUri);
            Assert.AreEqual(UserAgent, request.UserAgent);
        }
    }
}