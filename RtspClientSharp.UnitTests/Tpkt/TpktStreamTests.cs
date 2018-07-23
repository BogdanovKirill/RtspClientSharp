using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Tpkt;
using RtspClientSharp.Utils;

namespace RtspClientSharp.UnitTests.Tpkt
{
    [TestClass]
    public class TpktStreamTests
    {
        [TestMethod]
        public async Task ReadAsync_DamagedStream_SkipsWrongBytes()
        {
            var streamBytes = new byte[] {1, 2, 3, 4, 5, TpktHeader.Id, 1, 0, 1, 9};
            var ms = new MemoryStream(streamBytes);

            var tpktStream = new TpktStream(ms);
            TpktPayload payload = await tpktStream.ReadAsync();

            Assert.AreEqual(1, payload.Channel);
            Assert.AreEqual(1, payload.PayloadSegment.Count);
            Assert.AreEqual(9, payload.PayloadSegment[0]);
        }

        [TestMethod]
        public async Task WriteAsync_TestPayload_WritesWithValidHeader()
        {
            var testPayloadBytes = new byte[] {1, 2, 3, 4, 5, 6};
            var ms = new MemoryStream();
            var tpktStream = new TpktStream(ms);

            await tpktStream.WriteAsync(1, new ArraySegment<byte>(testPayloadBytes));
            byte[] writtenBytes = ms.ToArray();

            Assert.AreEqual((byte) TpktHeader.Id, writtenBytes[0]);
            Assert.AreEqual(1, writtenBytes[1]);
            Assert.AreEqual(testPayloadBytes.Length, writtenBytes[2] << 8 | writtenBytes[3]);
            Assert.IsTrue(testPayloadBytes.SequenceEqual(writtenBytes.Skip(4)));
        }
    }
}