using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;

namespace BuildPkiSample.Setup
{
    internal class AcquireTokenResult
    {
        public AcquireTokenResult(AzureCredentials credentials, string userObjectId)
        {
            Credentials = credentials;
            UserObjectId = userObjectId;
        }

        public AzureCredentials Credentials { get; }
        public string UserObjectId { get; }
    }
}