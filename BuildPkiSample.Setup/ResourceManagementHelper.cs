using System.Threading.Tasks;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.AppService.Fluent.Models;
using Microsoft.Azure.Management.KeyVault.Fluent;
using Microsoft.Azure.Management.KeyVault.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Rest;
using SkuName = Microsoft.Azure.Management.Storage.Fluent.Models.SkuName;

namespace BuildPkiSample.Setup
{
    internal class ResourceManagementHelper
    {
        private readonly Configuration _configuration;
        private readonly AzureCredentials _azureCredentials;
        private readonly string _currentUserObjectId;

        public ResourceManagementHelper(Configuration configuration, AcquireTokenResult acquireTokenResult)
        {
            _configuration = configuration;
            _azureCredentials = new AzureCredentials(
                new TokenCredentials(acquireTokenResult.AccessToken),
                new TokenCredentials(acquireTokenResult.AccessToken),
                configuration.TenantId,
                AzureEnvironment.AzureGlobalCloud);
            _currentUserObjectId = acquireTokenResult.UserObjectId;
        }

        public async Task CreateAzureResourcesAsync(bool alwaysCreate)
        {
            if (!alwaysCreate && await ResourceGroupExistsAsync())
            {
                return;
            }

            var resourceGroup = await CreateResourceGroupAsync();
            var functionApp = await CreateFunctionAppAsync(resourceGroup);
            await CreateVaultAsync(resourceGroup, functionApp.SystemAssignedManagedServiceIdentityPrincipalId);
        }

        private Task<bool> ResourceGroupExistsAsync()
        {
            return ResourceManager
                .Authenticate(_azureCredentials)
                .WithSubscription(_configuration.SubscriptionId)
                .ResourceGroups
                .ContainAsync(_configuration.ResourceGroupName);
        }

        private Task<IResourceGroup> CreateResourceGroupAsync()
        {
            return ResourceManager
                .Authenticate(_azureCredentials)
                .WithSubscription(_configuration.SubscriptionId)
                .ResourceGroups
                .Define(_configuration.ResourceGroupName)
                .WithRegion(_configuration.ResourceGroupLocation)
                .CreateAsync();
        }
        
        private async Task<IFunctionApp> CreateFunctionAppAsync(IResourceGroup resourceGroup)
        {
            var appServiceManager = AppServiceManager.Authenticate(_azureCredentials, _configuration.SubscriptionId);
            var appServicePlan = await appServiceManager
                .AppServicePlans
                .Define(_configuration.FunctionAppName + "Plan")
                .WithRegion(_configuration.ResourceGroupLocation)
                .WithExistingResourceGroup(resourceGroup)
                .WithPricingTier(PricingTier.FromSkuDescription(new SkuDescription("Y1", "Dynamic", "Y1", "Y", 0)))
                .WithOperatingSystem(OperatingSystem.Windows)
                .CreateAsync();

            return await AppServiceManager
                .Authenticate(_azureCredentials, _configuration.SubscriptionId)
                .FunctionApps
                .Define(_configuration.FunctionAppName)
                .WithExistingAppServicePlan(appServicePlan)
                .WithExistingResourceGroup(resourceGroup)
                .WithNewStorageAccount(_configuration.FunctionAppName.ToLowerInvariant(), SkuName.StandardLRS)
                .WithSystemAssignedManagedServiceIdentity()
                .CreateAsync();
        }

        private Task<IVault> CreateVaultAsync(IResourceGroup resourceGroup, string certificateAuthorityPrincipalId)
        {
            return KeyVaultManager
                .Authenticate(_azureCredentials, _configuration.SubscriptionId)
                .Vaults
                .Define(_configuration.VaultName)
                .WithRegion(_configuration.ResourceGroupLocation)
                .WithExistingResourceGroup(resourceGroup)
                .DefineAccessPolicy()
                .ForObjectId(_currentUserObjectId)
                .AllowCertificatePermissions(CertificatePermissions.List, CertificatePermissions.Get, 
                    CertificatePermissions.Create, CertificatePermissions.Update, CertificatePermissions.Delete)
                .Attach()
                .DefineAccessPolicy()
                .ForObjectId(certificateAuthorityPrincipalId)
                .AllowKeyPermissions(KeyPermissions.Sign)
                .Attach()
                .CreateAsync();
        }
    }
}