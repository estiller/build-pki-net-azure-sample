using System.Threading.Tasks;
using Microsoft.Azure.Management.KeyVault.Fluent;
using Microsoft.Azure.Management.KeyVault.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Extensions.Configuration;

namespace BuildPkiSample.Setup
{
    internal class Program
    {
        public static async Task Main()
        {
            var configuration = ReadConfiguration();
            AuthenticationResult authenticationResult = await new AuthenticationHelper(configuration.ClientId, configuration.TenantId).AcquireTokenAsync();
            var resourceGroup = await CreateResourceGroupAsync(authenticationResult, configuration);
            await CreateVaultAsync(authenticationResult, configuration, resourceGroup);
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

        private static Task<IResourceGroup> CreateResourceGroupAsync(AuthenticationResult authenticationResult,
            Configuration configuration)
        {
            return ResourceManager
                .Authenticate(authenticationResult.Credentials)
                .WithSubscription(configuration.SubscriptionId)
                .ResourceGroups
                .Define(configuration.ResourceGroupName)
                .WithRegion(configuration.ResourceGroupLocation)
                .CreateAsync();
        }


        private static Task<IVault> CreateVaultAsync(AuthenticationResult authenticationResult, Configuration configuration,
            IResourceGroup resourceGroup)
        {
            return KeyVaultManager
                .Authenticate(authenticationResult.Credentials, configuration.SubscriptionId)
                .Vaults
                .Define(configuration.VaultName)
                .WithRegion(configuration.ResourceGroupLocation)
                .WithExistingResourceGroup(resourceGroup)
                .DefineAccessPolicy()
                .ForObjectId(authenticationResult.UserObjectId)
                .AllowCertificatePermissions(CertificatePermissions.List, CertificatePermissions.Create,
                    CertificatePermissions.Update, CertificatePermissions.Delete)
                .Attach()
                .DefineAccessPolicy()
                .ForObjectId(configuration.CertificateAuthorityObjectId)
                .AllowKeyPermissions(KeyPermissions.Sign)
                .Attach()
                .CreateAsync();
        }
    }
}
