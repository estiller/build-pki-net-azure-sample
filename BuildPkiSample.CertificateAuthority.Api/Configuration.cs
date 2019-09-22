namespace BuildPkiSample.CertificateAuthority.Api
{
    internal class Configuration
    {
        public string RootCertificateId { get; set; } = default!;
        public string StorageConnectionString { get; set; } = default!;
        public string StorageContainerName { get; set; } = default!;
    }
}