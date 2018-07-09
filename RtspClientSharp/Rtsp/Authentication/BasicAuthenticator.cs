using System;
using System.Net;
using System.Text;

namespace RtspClientSharp.Rtsp.Authentication
{
    class BasicAuthenticator : Authenticator
    {
        public BasicAuthenticator(NetworkCredential credentials)
            : base(credentials)
        {
        }

        public override string GetResponse(uint nonceCounter, string uri, string method, byte[] entityBodyBytes)
        {
            string usernamePasswordHash = $"{Credentials.UserName}:{Credentials.Password}";

            return $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(usernamePasswordHash))}";
        }
    }
}