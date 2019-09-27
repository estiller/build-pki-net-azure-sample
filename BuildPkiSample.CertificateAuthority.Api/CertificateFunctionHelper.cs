using BuildPkiSample.CertificateAuthority.BusinessLogic;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;

namespace BuildPkiSample.CertificateAuthority.Api
{
    internal static class CertificateFunctionHelper
    {
        internal static CertificateIssuer CreateCertificateIssuer(string functionAppDirectory)
        {
            var configuration = ReadConfiguration(functionAppDirectory);
            var serialNumberGenerator = new SerialNumberGenerator(configuration.StorageConnectionString, configuration.StorageContainerName);
            return new CertificateIssuer(CreateKeyVaultClient(), configuration.RootCertificateId, serialNumberGenerator);
        }

        private static Configuration ReadConfiguration(string functionAppDirectory)
        {
            var configurationRoot = new ConfigurationBuilder()
                .SetBasePath(functionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
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