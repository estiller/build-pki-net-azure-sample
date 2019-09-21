using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;

namespace BuildPkiSample.CertificateAuthority.BusinessLogic
{
    public class CertificateIssuer
    {
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
            using RSA certificateKey = CreateCertificateKey(publicKey);
            CertificateRequest request = CreateCertificateRequest(subjectName, certificateKey);
            byte[] certificateSerialNumber = await _serialNumberGenerator.GenerateSerialAsync();

            var certificateBundle = await _keyVaultClient.GetCertificateAsync(_rootCertificateId);
            using var issuerCertificate = new X509Certificate2(certificateBundle.Cer);
            using var rsaKeyVault = _keyVaultClient.ToRSA(certificateBundle.KeyIdentifier, issuerCertificate);
            var generator = X509SignatureGenerator.CreateForRSA(rsaKeyVault, RSASignaturePadding.Pkcs1);
            return request.Create(issuerCertificate.SubjectName, generator, DateTime.Today, DateTime.Today.AddYears(1), certificateSerialNumber);
        }

        private static RSA CreateCertificateKey(RSAPublicKeyParameters publicKey)
        {
            var parameters = new RSAParameters { Modulus = publicKey.Modulus, Exponent = publicKey.Exponent };
            return RSA.Create(parameters);
        }

        private static CertificateRequest CreateCertificateRequest(string subjectName, RSA certificateKey)
        {
            var subjectDistinguishedName = new X500DistinguishedName("CN=" + subjectName);
            var request = new CertificateRequest(subjectDistinguishedName, certificateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(new OidCollection {new Oid("1.3.6.1.5.5.7.3.2")}, false));
            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            return request;
        }
    }
}
