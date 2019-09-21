using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace BuildPkiSample.Setup
{
    internal class Program
    {
        public static async Task Main()
        {
            var configuration = ReadConfiguration();
            AcquireTokenResult acquireTokenResult = await new AuthenticationHelper(configuration.ClientId, configuration.TenantId, AuthenticationHelper.AzureManagementScopes).AcquireTokenAsync();
            await new ResourceManagementHelper(configuration, acquireTokenResult).CreateAzureResourcesAsync(false);

            acquireTokenResult = await new AuthenticationHelper(configuration.ClientId, configuration.TenantId, AuthenticationHelper.KeyVaultScopes).AcquireTokenAsync();
            await new RootCertificateHelper(configuration, acquireTokenResult.AccessToken).GenerateRootCertificate();
        }

        private static Configuration ReadConfiguration()
        {
            var configurationRoot = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddUserSecrets<Program>()
                .Build();
            var configuration = configurationRoot.Get<Configuration>();
            return configuration;
        }
    }
}
