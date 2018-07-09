using System;
using System.Runtime.Serialization;

namespace SimpleRtspPlayer.RawFramesDecoding
{
    [Serializable]
    public class DecoderException : Exception
    {
        //
        // For guidelines regarding the creation of new exception types, see
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
        // and
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
        //

        public DecoderException()
        {
        }

        public DecoderException(string message) : base(message)
        {
        }

        public DecoderException(string message, Exception inner) : base(message, inner)
        {
        }

        protected DecoderException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}