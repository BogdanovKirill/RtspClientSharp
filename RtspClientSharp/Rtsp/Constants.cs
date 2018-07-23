namespace RtspClientSharp.Rtsp
{
    static class Constants
    {
        public const int DefaultHttpPort = 80;
        public const int DefaultRtspPort = 554;
        public static readonly byte[] RtspProtocolNameBytes = {(byte) 'R', (byte) 'T', (byte) 'S', (byte) 'P'};
        public const int MaxResponseHeadersSize = 8 * 1024;
        public static readonly byte[] DoubleCrlfBytes = {(byte) '\r', (byte) '\n', (byte) '\r', (byte) '\n'};
        public const int UdpReceiveBufferSize = 2048;
    }
}