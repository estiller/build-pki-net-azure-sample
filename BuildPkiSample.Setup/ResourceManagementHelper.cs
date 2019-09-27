using System;
using System.Threading.Tasks;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.AppService.Fluent.Models;
using Microsoft.Azure.Management.Graph.RBAC.Fluent;
using Microsoft.Azure.Management.KeyVault.Fluent;
using Microsoft.Azure.Management.KeyVault.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ServiceBus.Fluent;
using Microsoft.Azure.Management.Storage.Fluent;
using Microsoft.Rest;
using OperatingSystem = Microsoft.Azure.Management.AppService.Fluent.OperatingSystem;

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
                Console.WriteLine($"Resource group '{ResourceGroupName}' already exists. Skipping resource creation.");
                return;
            }

            var resourceGroup = await CreateResourceGroupAsync();
            var serviceBusQueue = await CreateServiceBusQueueAsync(resourceGroup);
            var functionApp = await CreateFunctionAppsAsync(resourceGroup, serviceBusQueue);
            await CreateVaultAsync(resourceGroup, functionApp.SystemAssignedManagedServiceIdentityPrincipalId);
        }

        private string ResourceGroupName => _configuration.ResourceNamePrefix;

        private Task<bool> ResourceGroupExistsAsync()
        {
            return ResourceManager
                .Authenticate(_azureCredentials)
                .WithSubscription(_configuration.SubscriptionId)
                .ResourceGroups
                .ContainAsync(ResourceGroupName);
        }

        private async Task<IResourceGroup> CreateResourceGroupAsync()
        {
            var resourceGroup = await ResourceManager
                .Authenticate(_azureCredentials)
                .WithSubscription(_configuration.SubscriptionId)
                .ResourceGroups
                .Define(ResourceGroupName)
                .WithRegion(_configuration.RegionName)
                .CreateAsync();
            Console.WriteLine($"Successfully created or updated resource group '{resourceGroup.Name}' in region '{resourceGroup.RegionName}'");
            return resourceGroup;
        }

        private async Task<IQueue> CreateServiceBusQueueAsync(IResourceGroup resourceGroup)
        {
            var serviceBus = await ServiceBusManager
                .Authenticate(_azureCredentials, _configuration.SubscriptionId)
                .Namespaces
                .Define(_configuration.ResourceNamePrefix + "Bus")
                .WithRegion(_configuration.RegionName)
                .WithExistingResourceGroup(resourceGroup)
                .WithSku(NamespaceSku.Basic)
                .WithNewQueue(_configuration.CertificateRenewalQueueName, 1024)
                .CreateAsync();
            Console.WriteLine($"Successfully created or updated service bus '{serviceBus.Name}'");
            return await serviceBus.Queues.GetByNameAsync(_configuration.CertificateRenewalQueueName);
        }

        private async Task<IFunctionApp> CreateFunctionAppsAsync(IResourceGroup resourceGroup, IQueue serviceBusQueue)
        {
            var storageAccount = await StorageManager
                .Authenticate(_azureCredentials, _configuration.SubscriptionId)
                .StorageAccounts
                .Define(_configuration.ResourceNamePrefix.ToLowerInvariant() + "storage")
                .WithRegion(_configuration.RegionName)
                .WithExistingResourceGroup(resourceGroup)
                .WithSku(StorageAccountSkuType.Standard_LRS)
                .CreateAsync();
            Console.WriteLine($"Successfully created or updated storage account '{storageAccount.Name}'");

            var appServiceManager = AppServiceManager.Authenticate(_azureCredentials, _configuration.SubscriptionId);
            var appServicePlan = await appServiceManager
                .AppServicePlans
                .Define(_configuration.ResourceNamePrefix + "Plan")
                .WithRegion(_configuration.RegionName)
                .WithExistingResourceGroup(resourceGroup)
                .WithPricingTier(PricingTier.FromSkuDescription(new SkuDescription("Y1", "Dynamic", "Y1", "Y", 0)))
                .WithOperatingSystem(OperatingSystem.Windows)
                .CreateAsync();
            Console.WriteLine($"Successfully created or updated app service plan '{appServicePlan.Name}'");

            var functionApp = await appServiceManager
                .FunctionApps
                .Define(_configuration.ResourceNamePrefix + "Api")
                .WithExistingAppServicePlan(appServicePlan)
                .WithExistingResourceGroup(resourceGroup)
                .WithExistingStorageAccount(storageAccount)
                .WithSystemAssignedManagedServiceIdentity()
                .WithSystemAssignedIdentityBasedAccessTo(serviceBusQueue.Id, BuiltInRole.Parse("Azure Service Bus Data Receiver"))
                .DefineAuthentication()
                .WithDefaultAuthenticationProvider(BuiltInAuthenticationProvider.AzureActiveDirectory)
                .WithActiveDirectory(_configuration.CertificateAuthorityClientId, "https://login.microsoftonline.com/" + _configuration.TenantId)
                .Attach()
                .CreateAsync();
            Console.WriteLine($"Successfully created or updated function app '{functionApp.Name}'");
            return functionApp;
        }

        private async Task CreateVaultAsync(IResourceGroup resourceGroup, string certificateAuthorityPrincipalId)
        {
            var vault = await KeyVaultManager
                .Authenticate(_azureCredentials, _configuration.SubscriptionId)
                .Vaults
                .Define(_configuration.ResourceNamePrefix + "Vault")
                .WithRegion(_configuration.RegionName)
                .WithExistingResourceGroup(resourceGroup)
                .DefineAccessPolicy()
                .ForObjectId(_currentUserObjectId)
                .AllowCertificatePermissions(CertificatePermissions.List, CertificatePermissions.Get, 
                    CertificatePermissions.Create, CertificatePermissions.Update, CertificatePermissions.Delete)
                .AllowKeyPermissions(KeyPermissions.Sign)  // This is required for local testing & debugging. Would remove for production.
                .Attach()
                .DefineAccessPolicy()
                .ForObjectId(certificateAuthorityPrincipalId)
                .AllowKeyPermissions(KeyPermissions.Sign)
                .AllowCertificatePermissions(CertificatePermissions.Get)
                .Attach()
                .CreateAsync();
            Console.WriteLine($"Successfully created or updated key vault '{vault.Name}'");
        }
    }
}