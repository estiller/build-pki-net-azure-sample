#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Rest;
using Newtonsoft.Json;

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

            return CreateCertificateInKeyVaultAsync(client);
            //return CreateSelfSignedCertificateAndUploadAsync(client);
        }

        // NOTE - if you'd like to create the root certificate locally with .NET then this would be the way to go.
        // However, this exposes the private key on the local machine which we'd rather avoid. Instead, use the alternative
        // of generating the root certificate directly on Azure Key-Vault.
        //
        // ReSharper disable once UnusedMember.Local
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

        private async Task<X509Certificate2> CreateCertificateInKeyVaultAsync(KeyVaultClient client)
        {
            var certificateOperation = await client.CreateCertificateAsync(
                _vaultBaseUrl,
                _certificateName,
                new CertificatePolicy(
                    keyProperties: new KeyProperties(false, "RSA", 2048, false),
                    x509CertificateProperties: new X509CertificatePropertiesEx(
                        "CN=" + RootSubjectName,
                        keyUsage: new List<string> {X509KeyUsageFlags.KeyCertSign.ToString()},
                        ekus: new List<string> {"1.3.6.1.5.5.7.3.2", "1.3.6.1.5.5.7.3.1"},
                        basicConstraints: new X509CertificatePropertiesEx.BasicConstraintsExtension(true, 1)),
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

        // This class is a hack to expose support for setting the "basic_constraints" certificate attribute.
        // See https://github.com/estiller/build-pki-net-azure-sample/issues/1
        // Future SDK versions might expose this natively so this class will become redundant
        [SuppressMessage("ReSharper", "IdentifierTypo")]
        private class X509CertificatePropertiesEx : X509CertificateProperties
        {
            public X509CertificatePropertiesEx(string? subject = null,
                IList<string>? ekus = null, 
                SubjectAlternativeNames? subjectAlternativeNames = null,
                IList<string>? keyUsage = null, 
                int? validityInMonths = null, 
                BasicConstraintsExtension? basicConstraints = null) 
                : base(subject, ekus, subjectAlternativeNames, keyUsage, validityInMonths)
            {
                BasicConstraints = basicConstraints;
            }

            [JsonProperty("basic_constraints")] public BasicConstraintsExtension? BasicConstraints { get; set; }

            public class BasicConstraintsExtension
            {
                public BasicConstraintsExtension(bool isCa, int pathLenConstraint)
                {
                    IsCA = isCa;
                    PathLenConstraint = pathLenConstraint;
                }

                // ReSharper disable once InconsistentNaming
                [JsonProperty("ca")] public bool IsCA { get; set; }
                [JsonProperty("path_len_constraint")] public int PathLenConstraint { get; set; }
            }
        }
    }
}