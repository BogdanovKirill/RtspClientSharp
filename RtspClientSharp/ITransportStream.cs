using System;

namespace RtspClientSharp
{
    interface ITransportStream
    {
        void Process(ArraySegment<byte> payloadSegment);
    }
}