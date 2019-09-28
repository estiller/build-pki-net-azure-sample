using System;
using System.Security.Cryptography;

namespace BuildPkiSample.Clients.RequestCertificate
{
    internal class IssueCertificateRequest
    {
        public IssueCertificateRequest(string subjectName, RSAParameters publicParameters)
        {
            SubjectName = subjectName;
            PublicKey = new PublicKeyParameters(publicParameters);
        }

        public string SubjectName { get; }

        public PublicKeyParameters PublicKey { get; }

        public class PublicKeyParameters
        {
            public PublicKeyParameters(RSAParameters publicParameters)
            {
                Exponent = Convert.ToBase64String(publicParameters.Exponent);
                Modulus = Convert.ToBase64String(publicParameters.Modulus);
            }

            public string Exponent { get; }
            public string Modulus { get; }
        }
    }
}