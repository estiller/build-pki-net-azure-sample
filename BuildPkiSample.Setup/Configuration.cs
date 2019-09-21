namespace BuildPkiSample.Setup
{
    public class Configuration
    {
        public string ClientId { get; set; } = default!;
        public string TenantId { get; set; } = default!;
        public string SubscriptionId { get; set; } = default!;
        public string ResourceNamePrefix { get; set; } = default!;
        public string RegionName { get; set; } = default!;
        public string CertificateAuthorityClientId { get; set; } = default!;
    }
}