using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RtspClientSharp.Utils;

namespace RtspClientSharp.UnitTests.Utils
{
    [TestClass]
    public class StreamExtensionsTests
    {
        private class FakeStream : MemoryStream
        {
            public FakeStream(byte[] buffer) : base(buffer)
            {
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count,
                CancellationToken cancellationToken)
            {
                return base.ReadAsync(buffer, offset, 1, cancellationToken);
            }
        }

        [TestMethod]
        public async Task ReadExactAsync_OneBytesPerReadFromStream_ValidResult()
        {
            var inputBuffer = new byte[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 10};
            var readBuffer = new byte[inputBuffer.Length];
            var stream = new FakeStream(inputBuffer);

            await stream.ReadExactAsync(readBuffer, 0, inputBuffer.Length);

            Assert.IsTrue(inputBuffer.SequenceEqual(readBuffer));
        }
    }
}