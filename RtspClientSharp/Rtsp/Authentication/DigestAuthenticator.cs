using System;
using System.Net;
using System.Text;

namespace RtspClientSharp.Rtsp.Authentication
{
    class DigestAuthenticator : Authenticator
    {
        private readonly string _realm;
        private readonly string _nonce;
        private readonly string _qop;
        private readonly string _cnonce;

        public DigestAuthenticator(NetworkCredential credentials, string realm, string nonce, string qop)
            : base(credentials)
        {
            _realm = realm ?? throw new ArgumentNullException(nameof(realm));
            _nonce = nonce ?? throw new ArgumentNullException(nameof(nonce));

            if (qop != null)
            {
                int commaIndex = qop.IndexOf(',');

                _qop = commaIndex != -1 ? qop.Substring(0, commaIndex) : qop;
            }

            uint cnonce = (uint) Guid.NewGuid().GetHashCode();
            _cnonce = cnonce.ToString("X8");
        }

        public override string GetResponse(uint nonceCounter, string uri, string method, byte[] entityBodyBytes)
        {
            string ha1 = MD5.GetHashHexValues(Credentials.UserName + ":" + _realm + ":" + Credentials.Password);

            string ha2Argument = method + ":" + uri;

            bool hasQop = !string.IsNullOrEmpty(_qop);

            if (hasQop && _qop.Equals("auth-int", StringComparison.InvariantCultureIgnoreCase))
                ha2Argument += ":" + MD5.GetHashHexValues(entityBodyBytes);

            string ha2 = MD5.GetHashHexValues(ha2Argument);

            string response;
            var sb = new StringBuilder();

            sb.AppendFormat("Digest username=\"{0}\", realm=\"{1}\", nonce=\"{2}\", uri=\"{3}\", ",
                Credentials.UserName, _realm, _nonce, uri);

            if (!hasQop)
            {
                response = MD5.GetHashHexValues(ha1 + ":" + _nonce + ":" + ha2);
                sb.AppendFormat("response=\"{0}\"", response);
            }
            else
            {
                response = MD5.GetHashHexValues(ha1 + ":" + _nonce + ":" + nonceCounter.ToString("X8") + ":" + _cnonce +
                                                ":" + _qop + ":" + ha2);
                sb.AppendFormat("response=\"{0}\", cnonce=\"{1}\", nc=\"{2:X8}\", qop=\"{3}\"", response, _cnonce,
                    nonceCounter, _qop);
            }

            return sb.ToString();
        }
    }
}