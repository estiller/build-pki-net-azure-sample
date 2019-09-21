using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BuildPkiSample.CertificateAuthority.BusinessLogic.IntegrationTests
{
    public class CertificateIssuerTests
    {
        private static readonly Configuration Configuration = ReadConfiguration();
        private readonly KeyVaultClient _client = CreateKeyVaultClient();

        [Fact]
        public async Task IssueCertificateTest()
        {
            var serialNumberGenerator = new SerialNumberGenerator(Configuration.StorageConnectionString, Configuration.StorageContainerName);
            var issuer = new CertificateIssuer(_client, Configuration.RootCertificateId, serialNumberGenerator);

            X509Certificate2 certificate = await issuer.IssueCertificateAsync("Test Certificate", GeneratePublicKeyParameters());
            Assert.Equal("CN=Test Certificate", certificate.Subject);
        }

        private static RSAPublicKeyParameters GeneratePublicKeyParameters()
        {
            using var certificateKey = RSA.Create();
            var parameters = certificateKey.ExportParameters(false);
            return new RSAPublicKeyParameters(parameters.Exponent, parameters.Modulus);
        }

        private static Configuration ReadConfiguration()
        {
            var configurationRoot = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddUserSecrets<CertificateIssuerTests>()
                .Build();
            var configuration = configurationRoot.Get<Configuration>();
            return configuration;
        }

        private static KeyVaultClient CreateKeyVaultClient()
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            return new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));
        }
    }
}
