using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;

namespace BuildPkiSample.CertificateAuthority.BusinessLogic
{
    public class CertificateIssuer
    {
        private const string SubjectIdExtensionOid = "2.5.29.14";
        private const string AuthorityIdExtensionOid = "2.5.29.35";

        private readonly KeyVaultClient _keyVaultClient;
        private readonly string _rootCertificateId;
        private readonly SerialNumberGenerator _serialNumberGenerator;

        public CertificateIssuer(KeyVaultClient keyVaultClient, string rootCertificateId, SerialNumberGenerator serialNumberGenerator)
        {
            _keyVaultClient = keyVaultClient;
            _rootCertificateId = rootCertificateId;
            _serialNumberGenerator = serialNumberGenerator;
        }

        public async Task<X509Certificate2> IssueCertificateAsync(string subjectName, RSAPublicKeyParameters publicKey)
        {
            var certificateBundle = await _keyVaultClient.GetCertificateAsync(_rootCertificateId);
            using var issuerCertificate = new X509Certificate2(certificateBundle.Cer);

            using RSA certificateKey = CreateCertificateKey(publicKey);
            CertificateRequest request = CreateCertificateRequest(subjectName, certificateKey, issuerCertificate.Extensions[SubjectIdExtensionOid]);
            byte[] certificateSerialNumber = await _serialNumberGenerator.GenerateSerialAsync();

            using var rsaKeyVault = _keyVaultClient.ToRSA(certificateBundle.KeyIdentifier, issuerCertificate);
            var generator = X509SignatureGenerator.CreateForRSA(rsaKeyVault, RSASignaturePadding.Pkcs1);
            return request.Create(issuerCertificate.SubjectName, generator, DateTime.Today.AddDays(-1), DateTime.Today.AddYears(1), certificateSerialNumber);
        }

        private static RSA CreateCertificateKey(RSAPublicKeyParameters publicKey)
        {
            var parameters = new RSAParameters { Modulus = publicKey.Modulus, Exponent = publicKey.Exponent };
            return RSA.Create(parameters);
        }

        private static CertificateRequest CreateCertificateRequest(string subjectName, RSA certificateKey, X509Extension authorityKeyIdentifierExtension)
        {
            var subjectDistinguishedName = new X500DistinguishedName("CN=" + subjectName);
            var request = new CertificateRequest(subjectDistinguishedName, certificateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, true, 0, true));
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(new OidCollection {new Oid("1.3.6.1.5.5.7.3.2"), new Oid("1.3.6.1.5.5.7.3.1")}, false));
            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            request.CertificateExtensions.Add(BuildAuthorityKeyIdentifierExtension(authorityKeyIdentifierExtension));
            return request;
        }

        // There is no built-in support in .NET, so it needs to be copied from the Subject Key 
        // Identifier of the signing certificate and massaged slightly.
        // Inspired by https://blog.rassie.dk/2018/04/creating-an-x-509-certificate-chain-in-c/
        private static X509Extension BuildAuthorityKeyIdentifierExtension(X509Extension authorityKeyIdentifierExtension)
        {
            var authoritySubjectKey = authorityKeyIdentifierExtension.RawData;
            var segment = new Span<byte>(authoritySubjectKey, 2, authoritySubjectKey.Length - 2);
            var authorityKeyIdentifier = new byte[segment.Length + 4];
            // these bytes define the "KeyID" part of the AuthorityKeyIdentifier
            authorityKeyIdentifier[0] = 0x30;
            authorityKeyIdentifier[1] = 0x16;
            authorityKeyIdentifier[2] = 0x80;
            authorityKeyIdentifier[3] = 0x14;
            segment.CopyTo(new Span<byte>(authorityKeyIdentifier, 4, authorityKeyIdentifier.Length - 4));
            return new X509Extension(AuthorityIdExtensionOid, authorityKeyIdentifier, false);
        }
    }
}
