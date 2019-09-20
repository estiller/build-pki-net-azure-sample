using System.Threading.Tasks;
using Microsoft.Azure.Management.KeyVault.Fluent;
using Microsoft.Azure.Management.KeyVault.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;

namespace BuildPkiSample.Setup
{
    internal class ResourceManagementHelper
    {
        private readonly AcquireTokenResult _acquireTokenResult;
        private readonly Configuration _configuration;

        public ResourceManagementHelper(AcquireTokenResult acquireTokenResult, Configuration configuration)
        {
            _acquireTokenResult = acquireTokenResult;
            _configuration = configuration;
        }

        public async Task<CreateAzureResourcesResult> CreateAzureResourcesAsync()
        {
            var resourceGroup = await CreateResourceGroupAsync();
            var vault = await CreateVaultAsync(resourceGroup);
            return new CreateAzureResourcesResult(resourceGroup, vault);
        }

        private Task<IResourceGroup> CreateResourceGroupAsync()
        {
            return ResourceManager
                .Authenticate(_acquireTokenResult.Credentials)
                .WithSubscription(_configuration.SubscriptionId)
                .ResourceGroups
                .Define(_configuration.ResourceGroupName)
                .WithRegion(_configuration.ResourceGroupLocation)
                .CreateAsync();
        }


        private Task<IVault> CreateVaultAsync(IResourceGroup resourceGroup)
        {
            return KeyVaultManager
                .Authenticate(_acquireTokenResult.Credentials, _configuration.SubscriptionId)
                .Vaults
                .Define(_configuration.VaultName)
                .WithRegion(_configuration.ResourceGroupLocation)
                .WithExistingResourceGroup(resourceGroup)
                .DefineAccessPolicy()
                .ForObjectId(_acquireTokenResult.UserObjectId)
                .AllowCertificatePermissions(CertificatePermissions.List, CertificatePermissions.Create,
                    CertificatePermissions.Update, CertificatePermissions.Delete)
                .Attach()
                .DefineAccessPolicy()
                .ForObjectId(_configuration.CertificateAuthorityObjectId)
                .AllowKeyPermissions(KeyPermissions.Sign)
                .Attach()
                .CreateAsync();
        }

    }
}