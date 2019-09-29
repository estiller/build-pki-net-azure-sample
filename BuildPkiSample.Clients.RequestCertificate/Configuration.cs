namespace BuildPkiSample.Clients.RequestCertificate
{
    internal class Configuration
    {
        public string ClientId { get; set; } = default!;
        public string TenantId { get; set; } = default!;
        public string CertificateAuthorityScope { get; set; } = default!;
        public string BaseUrl { get; set; } = default!;
        public string DeviceName { get; set; } = default!;
    }
}