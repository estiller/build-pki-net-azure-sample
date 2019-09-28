using System;
using System.Threading.Tasks;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.AppService.Fluent.Models;
using Microsoft.Azure.Management.DeviceProvisioningServices;
using Microsoft.Azure.Management.DeviceProvisioningServices.Models;
using Microsoft.Azure.Management.IotHub;
using Microsoft.Azure.Management.IotHub.Models;
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
        private readonly string _accessToken;
        private readonly AzureCredentials _azureCredentials;
        private readonly string _currentUserObjectId;

        public ResourceManagementHelper(Configuration configuration, AcquireTokenResult acquireTokenResult)
        {
            _configuration = configuration;
            _accessToken = acquireTokenResult.AccessToken;
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
            var listenConnectionString = await CreateServiceBusQueueAsync(resourceGroup);
            var functionApp = await CreateFunctionAppsAsync(resourceGroup, listenConnectionString);
            await CreateVaultAsync(resourceGroup, functionApp.SystemAssignedManagedServiceIdentityPrincipalId);
            await CreateIotHub(resourceGroup);
            await CreateDeviceProvisioningService(resourceGroup);
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

        private async Task<string> CreateServiceBusQueueAsync(IResourceGroup resourceGroup)
        {
            var serviceBus = await ServiceBusManager
                .Authenticate(_azureCredentials, _configuration.SubscriptionId)
                .Namespaces
                .Define(_configuration.ResourceNamePrefix + "Bus")
                .WithRegion(_configuration.RegionName)
                .WithExistingResourceGroup(resourceGroup)
                .WithSku(NamespaceSku.Basic)
                .CreateAsync();
            Console.WriteLine($"Successfully created or updated service bus '{serviceBus.Name}'");

            var queue = await serviceBus
                .Queues
                .Define(_configuration.CertificateRenewalQueue.Name)
                .WithDefaultMessageTTL(TimeSpan.FromHours(1))
                .WithNewListenRule(_configuration.CertificateRenewalQueue.ListenPolicyName)
                .WithNewSendRule(_configuration.CertificateRenewalQueue.SendPolicyName)
                .CreateAsync();
            Console.WriteLine($"Successfully created or updated service bus queue '{queue.Name}'");

            var listenPolicy = await queue.AuthorizationRules.GetByNameAsync(_configuration.CertificateRenewalQueue.ListenPolicyName);
            var keys = await listenPolicy.GetKeysAsync();
            return keys.PrimaryConnectionString;
        }

        private async Task<IFunctionApp> CreateFunctionAppsAsync(IResourceGroup resourceGroup, string serviceBusQueueConnectionString)
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
                .DefineAuthentication()
                .WithDefaultAuthenticationProvider(BuiltInAuthenticationProvider.AzureActiveDirectory)
                .WithActiveDirectory(_configuration.CertificateAuthorityClientId, $"https://login.microsoftonline.com/{_configuration.TenantId}/v2.0")
                .Attach()
                .WithAppSetting("StorageConnectionString", await BuildStorageConnectionString())
                .WithAppSetting("StorageContainerName", _configuration.FunctionStorageContainerName)
                .WithAppSetting("RootCertificateId", BuildRootCertificateId())
                .WithAppSetting("ServiceBusQueueConnection", serviceBusQueueConnectionString)
                .CreateAsync();
            Console.WriteLine($"Successfully created or updated function app '{functionApp.Name}'");
            return functionApp;

            async Task<string> BuildStorageConnectionString()
            {
                var storageAccountKeys = await storageAccount.GetKeysAsync();
                var storageAccountKeyValue = storageAccountKeys[0].Value;
                return $"DefaultEndpointsProtocol=https;AccountName={storageAccount.Name};AccountKey={storageAccountKeyValue};EndpointSuffix=core.windows.net";
            }

            string BuildRootCertificateId() => $"https://{_configuration.ResourceNamePrefix.ToLowerInvariant()}vault.vault.azure.net/certificates/{_configuration.RootCertificateName.ToLowerInvariant()}";
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

        private async Task CreateIotHub(IResourceGroup resourceGroup)
        {
            var client = new IotHubClient(new TokenCredentials(_accessToken))
            {
                SubscriptionId = _configuration.SubscriptionId
            };
            var iotHub = await client.IotHubResource.BeginCreateOrUpdateAsync(
                resourceGroup.Name,
                _configuration.ResourceNamePrefix + "Hub",
                new IotHubDescription(
                    _configuration.RegionName,
                    new IotHubSkuInfo("F1", IotHubSkuTier.Free, 1)));
            Console.WriteLine($"Successfully created or updated Iot Hub '{iotHub.Name}'");
        }
        private async Task CreateDeviceProvisioningService(IResourceGroup resourceGroup)
        {
            var client = new IotDpsClient(new TokenCredentials(_accessToken))
            {
                SubscriptionId = _configuration.SubscriptionId
            }; 
            var deviceProvisioningService = await client.IotDpsResource.BeginCreateOrUpdateAsync(
                resourceGroup.Name,
                _configuration.ResourceNamePrefix + "Dps",
                new ProvisioningServiceDescription(
                    _configuration.RegionName,
                    new IotDpsPropertiesDescription(), 
                    new IotDpsSkuInfo("S1", "Standard", 1)));
            Console.WriteLine($"Successfully created or updated Device Provisioning Service '{deviceProvisioningService.Name}'");
        }
    }
}