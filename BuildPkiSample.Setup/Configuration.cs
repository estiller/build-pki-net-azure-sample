namespace BuildPkiSample.Setup
{
    public class Configuration
    {
        public string ClientId { get; set; } = default!;
        public string TenantId { get; set; } = default!;
        public string SubscriptionId { get; set; } = default!;
        public string ResourceGroupName { get; set; } = default!;
        public string ResourceGroupLocation { get; set; } = default!;
        public string VaultName { get; set; } = default!;
        public string CertificateAuthorityObjectId { get; set; } = default!;
    }
}