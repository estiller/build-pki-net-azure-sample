using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.KeyVault.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;

namespace BuildPkiSample.Setup
{
    internal class CreateAzureResourcesResult
    {
        public CreateAzureResourcesResult(IResourceGroup resourceGroup, IFunctionApp functionApp, IVault vault)
        {
            ResourceGroup = resourceGroup;
            FunctionApp = functionApp;
            Vault = vault;
        }

        public IResourceGroup ResourceGroup { get; }
        public IFunctionApp FunctionApp { get; }
        public IVault Vault { get; }
    }
}