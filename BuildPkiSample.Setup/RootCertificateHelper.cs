using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Rest;

namespace BuildPkiSample.Setup
{
    public class RootCertificateHelper
    {
        private const string RootSubjectName = "Build PKI Sample CA";

        private readonly string _vaultBaseUrl;
        private readonly string _certificateName;
        private readonly string _accessToken;

        public RootCertificateHelper(Configuration configuration, string accessToken)
        {
            _vaultBaseUrl = $"https://{configuration.ResourceNamePrefix.ToLowerInvariant()}vault.vault.azure.net/";
            _certificateName = configuration.RootCertificateName;
            _accessToken = accessToken;
        }

        public Task<X509Certificate2> GenerateRootCertificate()
        {
            var client = new KeyVaultClient(new TokenCredentials(_accessToken));

            //return CreateCertificateInKeyVaultAsync(client);
            return CreateSelfSignedCertificateAndUploadAsync(client);
        }

        private Task<X509Certificate2> CreateSelfSignedCertificateAndUploadAsync(KeyVaultClient client)
        {
            var certificate = CreateSelfSignedCertificateAndUpload();
            return ImportCertificateToKeyVaultAsync(client, certificate);
        }

        private X509Certificate2 CreateSelfSignedCertificateAndUpload()
        {
            var certificateKey = RSA.Create();
            var subjectDistinguishedName = new X500DistinguishedName("CN=" + RootSubjectName);
            var request = new CertificateRequest(subjectDistinguishedName, certificateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(true, true, 1, true));
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.2"), new Oid("1.3.6.1.5.5.7.3.1") }, false));
            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            return request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
        }

        private async Task<X509Certificate2> ImportCertificateToKeyVaultAsync(KeyVaultClient client, X509Certificate2 certificate)
        {
            CertificateBundle certificateBundle = await client.ImportCertificateAsync(
                _vaultBaseUrl,
                _certificateName,
                new X509Certificate2Collection(certificate),
                new CertificatePolicy(
                    keyProperties: new KeyProperties(false, "RSA", 2048, false),
                    secretProperties: new SecretProperties("application/x-pkcs12")));
            return new X509Certificate2(certificateBundle.Cer);
        }

        // NOTE - this would be the preferred way to create the certificate in Azure Key Vault, as the certificate's private key never leaves
        // the service. However, it does not currently support specifying that we want to create a CA certificate using the certificate basic constraints.
        // This code is left here for reference.
        //
        // ReSharper disable once UnusedMember.Local
        private async Task<X509Certificate2> CreateCertificateInKeyVaultAsync(KeyVaultClient client)
        {
            var certificateOperation = await client.CreateCertificateAsync(
                _vaultBaseUrl,
                _certificateName,
                new CertificatePolicy(
                    keyProperties: new KeyProperties(false, "RSA", 2048, false),
                    x509CertificateProperties: new X509CertificateProperties(
                        "CN=" + RootSubjectName,
                        keyUsage: new List<string> {X509KeyUsageFlags.KeyCertSign.ToString()},
                        ekus: new List<string> {"1.3.6.1.5.5.7.3.2", "1.3.6.1.5.5.7.3.1"}),
                    issuerParameters: new IssuerParameters("Self")));

            while (certificateOperation.Status == "inProgress")
            {
                Console.WriteLine($"Creation of certificate '{_certificateName}' is in progress");
                await Task.Delay(1000);
                certificateOperation = await client.GetCertificateOperationAsync(_vaultBaseUrl, _certificateName);
            }

            Console.WriteLine($"Creation of certificate '{_certificateName}' is in status '{certificateOperation.Status}'");

            var certificate = await client.GetCertificateAsync(_vaultBaseUrl, _certificateName);
            return new X509Certificate2(certificate.Cer);
        }
    }
}