using Microsoft.Azure.Management.KeyVault.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;

namespace BuildPkiSample.Setup
{
    internal class CreateAzureResourcesResult
    {
        public CreateAzureResourcesResult(IResourceGroup resourceGroup, IVault vault)
        {
            ResourceGroup = resourceGroup;
            Vault = vault;
        }

        public IResourceGroup ResourceGroup { get; }

        public IVault Vault { get; }
    }
}