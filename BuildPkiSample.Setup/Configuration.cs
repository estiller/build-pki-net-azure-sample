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
        public CertificateRenewalQueueConfiguration CertificateRenewalQueue { get; set; } = default!;
        public string RootCertificateName { get; set; } = default!;

        public class CertificateRenewalQueueConfiguration
        {
            public string Name { get; set; } = default!;
            public string ListenPolicyName { get; set; } = default!;
            public string SendPolicyName { get; set; } = default!;
        }
    }
}