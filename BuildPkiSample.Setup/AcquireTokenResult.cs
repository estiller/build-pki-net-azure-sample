using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;

namespace BuildPkiSample.Setup
{
    internal class AcquireTokenResult
    {
        public AcquireTokenResult(string accessToken, AzureCredentials credentials, string userObjectId)
        {
            AccessToken = accessToken;
            Credentials = credentials;
            UserObjectId = userObjectId;
        }

        public string AccessToken { get; }
        public AzureCredentials Credentials { get; }
        public string UserObjectId { get; }
    }
}