using System.Threading.Tasks;
using Microsoft.Azure.Management.AppService.Fluent;
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
            var functionApp = await CreateFunctionAppAsync(resourceGroup);
            var vault = await CreateVaultAsync(resourceGroup, functionApp.SystemAssignedManagedServiceIdentityPrincipalId);
            return new CreateAzureResourcesResult(resourceGroup, functionApp, vault);
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
        
        private Task<IFunctionApp> CreateFunctionAppAsync(IResourceGroup resourceGroup)
        {
            return AppServiceManager
                .Authenticate(_acquireTokenResult.Credentials, _configuration.SubscriptionId)
                .FunctionApps
                .Define(_configuration.FunctionAppName)
                .WithRegion(_configuration.ResourceGroupLocation)
                .WithExistingResourceGroup(resourceGroup)
                .WithSystemAssignedManagedServiceIdentity()
                .CreateAsync();
        }

        private Task<IVault> CreateVaultAsync(IResourceGroup resourceGroup, string certificateAuthorityPrincipalId)
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
                .ForObjectId(certificateAuthorityPrincipalId)
                .AllowKeyPermissions(KeyPermissions.Sign)
                .Attach()
                .CreateAsync();
        }
    }
}