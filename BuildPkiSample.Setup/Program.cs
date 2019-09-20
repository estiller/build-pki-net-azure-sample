using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace BuildPkiSample.Setup
{
    internal class Program
    {
        public static async Task Main()
        {
            var configuration = ReadConfiguration();
            AcquireTokenResult acquireTokenResult = await new AuthenticationHelper(configuration.ClientId, configuration.TenantId).AcquireTokenAsync();
            await new ResourceManagementHelper(acquireTokenResult, configuration).CreateAzureResourcesAsync();
        }

        private static Configuration ReadConfiguration()
        {
            var configurationRoot = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddUserSecrets<Program>()
                .AddEnvironmentVariables()
                .Build();
            var configuration = configurationRoot.Get<Configuration>();
            return configuration;
        }
    }
}
