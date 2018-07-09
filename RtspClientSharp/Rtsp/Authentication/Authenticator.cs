using System;
using System.Collections.Generic;
using System.Net;

namespace RtspClientSharp.Rtsp.Authentication
{
    abstract class Authenticator
    {
        public NetworkCredential Credentials { get; }

        protected Authenticator(NetworkCredential credentials)
        {
            Credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        }

        public abstract string GetResponse(uint nonceCounter, string uri, string method, byte[] entityBodyBytes);

        public static Authenticator Create(NetworkCredential credential, string authenticateHeader)
        {
            authenticateHeader = authenticateHeader ??
                                 throw new ArgumentNullException(nameof(authenticateHeader));

            if (authenticateHeader.StartsWith("Basic", StringComparison.OrdinalIgnoreCase))
                return new BasicAuthenticator(credential);

            if (authenticateHeader.StartsWith("Digest", StringComparison.OrdinalIgnoreCase))
            {
                int spaceIndex = authenticateHeader.IndexOf(' ');

                if (spaceIndex != -1)
                {
                    string parameters = authenticateHeader.Substring(++spaceIndex);

                    Dictionary<string, string> parameterNameToValueMap = ParseParameters(parameters);

                    if (!parameterNameToValueMap.TryGetValue("REALM", out string realm))
                        throw new ArgumentException("\"realm\" parameter is not found");
                    if (!parameterNameToValueMap.TryGetValue("NONCE", out string nonce))
                        throw new ArgumentException("\"nonce\" parameter is not found");

                    parameterNameToValueMap.TryGetValue("QOP", out string qop);
                    return new DigestAuthenticator(credential, realm, nonce, qop);
                }
            }

            throw new ArgumentOutOfRangeException(authenticateHeader,
                $"Invalid authenticate header: {authenticateHeader}");
        }

        private static Dictionary<string, string> ParseParameters(string parameters)
        {
            var parameterNameToValueMap = new Dictionary<string, string>();

            int parameterStartOffset = 0;
            while (parameterStartOffset < parameters.Length)
            {
                int equalsSignIndex = parameters.IndexOf('=', parameterStartOffset);

                if (equalsSignIndex == -1)
                    break;

                int parameterNameLength = equalsSignIndex - parameterStartOffset;
                string parameterName = parameters.Substring(parameterStartOffset, parameterNameLength).Trim()
                    .ToUpperInvariant();

                ++equalsSignIndex;

                int nonSpaceIndex = equalsSignIndex;

                if (nonSpaceIndex == parameters.Length)
                    break;

                while (parameters[nonSpaceIndex] == ' ')
                    if (++nonSpaceIndex == parameters.Length)
                        break;

                int parameterValueStartPos;
                int parameterValueEndPos;
                int commaIndex;

                if (parameters[nonSpaceIndex] == '\"')
                {
                    parameterValueStartPos = parameters.IndexOf('\"', equalsSignIndex);

                    if (parameterValueStartPos == -1)
                        break;

                    ++parameterValueStartPos;

                    parameterValueEndPos = parameters.IndexOf('\"', parameterValueStartPos);

                    if (parameterValueEndPos == -1)
                        break;

                    commaIndex = parameters.IndexOf(',', parameterValueEndPos + 1);

                    if (commaIndex != -1)
                        parameterStartOffset = ++commaIndex;
                    else
                        parameterStartOffset = parameters.Length;
                }
                else
                {
                    parameterValueStartPos = nonSpaceIndex;

                    commaIndex = parameters.IndexOf(',', ++nonSpaceIndex);

                    if (commaIndex != -1)
                    {
                        parameterValueEndPos = commaIndex;
                        parameterStartOffset = ++commaIndex;
                    }
                    else
                    {
                        parameterValueEndPos = parameters.Length;
                        parameterStartOffset = parameterValueEndPos;
                    }
                }

                int parameterValueLength = parameterValueEndPos - parameterValueStartPos;
                string parameterValue = parameters.Substring(parameterValueStartPos, parameterValueLength);

                parameterNameToValueMap[parameterName] = parameterValue;
            }

            return parameterNameToValueMap;
        }
    }
}