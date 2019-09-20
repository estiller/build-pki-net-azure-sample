namespace BuildPkiSample.Setup
{
    internal class AcquireTokenResult
    {
        public AcquireTokenResult(string accessToken, string userObjectId)
        {
            AccessToken = accessToken;
            UserObjectId = userObjectId;
        }

        public string AccessToken { get; }
        public string UserObjectId { get; }
    }
}