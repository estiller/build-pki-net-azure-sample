using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Rest;

namespace BuildPkiSample.Setup
{
    public class RootCertificateHelper
    {
        private readonly string _vaultBaseUrl;
        private readonly string _certificateName;
        private readonly string _accessToken;

        public RootCertificateHelper(Configuration configuration, string accessToken)
        {
            _vaultBaseUrl = $"https://{configuration.VaultName.ToLowerInvariant()}.vault.azure.net/";
            _certificateName = configuration.CertificateName;
            _accessToken = accessToken;
        }

        public async Task GenerateRootCertificate()
        {
            var client = new KeyVaultClient(new TokenCredentials(_accessToken));

            var certificateOperation = await client.CreateCertificateAsync(
                _vaultBaseUrl, 
                _certificateName,
                new CertificatePolicy(
                    keyProperties: new KeyProperties(false, "RSA", 2048, false),
                    x509CertificateProperties: new X509CertificateProperties(
                        "CN=Sample CA",
                        keyUsage: new List<string> {X509KeyUsageFlags.DigitalSignature.ToString()},
                        ekus: new List<string>()),
                    issuerParameters: new IssuerParameters("Self")));

            while (certificateOperation.Status == "inProgress")
            {
                Console.WriteLine($"Creation of certificate '{_certificateName}' is in progress");
                await Task.Delay(1000);
                certificateOperation = await client.GetCertificateOperationAsync(_vaultBaseUrl, _certificateName);
            }
            Console.WriteLine($"Creation of certificate '{_certificateName}' is in status '{certificateOperation.Status}'");
        }
    }
}