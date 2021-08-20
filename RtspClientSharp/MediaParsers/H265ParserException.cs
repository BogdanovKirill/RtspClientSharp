using System;
using System.Runtime.Serialization;

namespace RtspClientSharp.MediaParsers
{
    [Serializable]
    public class H265ParserException : Exception
    {
        public H265ParserException()
        {
        }

        public H265ParserException(string message) : base(message)
        {
        }

        public H265ParserException(string message, Exception inner) : base(message, inner)
        {
        }

        protected H265ParserException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}
