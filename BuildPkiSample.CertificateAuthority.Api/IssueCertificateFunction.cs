using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using BuildPkiSample.CertificateAuthority.BusinessLogic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BuildPkiSample.CertificateAuthority.Api
{
    public static class IssueCertificateFunction
    {
        [FunctionName("IssueCertificate")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "issueCertificate")]
            HttpRequest req, ExecutionContext context, ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            var certificateIssuer = CreateCertificateIssuer(context.FunctionAppDirectory);

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            var (subjectName, publicKey) = ExtractData(requestBody);
            var certificate = await certificateIssuer.IssueCertificateAsync(subjectName, publicKey);

            byte[] certificateBuffer = certificate.Export(X509ContentType.Cert);
            string encodedCertificate = Convert.ToBase64String(certificateBuffer);
            return new OkObjectResult(new { certificate = encodedCertificate });
        }

        private static (string subjectName, RSAPublicKeyParameters publicKey) ExtractData(string requestBody)
        {
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            string subjectName = data.subjectName;
            byte[] exponent = Convert.FromBase64String((string) data.publicKey.exponent);
            byte[] modulus = Convert.FromBase64String((string) data.publicKey.modulus);
            var publicKey = new RSAPublicKeyParameters(exponent, modulus);
            return (subjectName, publicKey);
        }

        private static CertificateIssuer CreateCertificateIssuer(string functionAppDirectory)
        {
            var configuration = ReadConfiguration(functionAppDirectory);
            var serialNumberGenerator = new SerialNumberGenerator(configuration.StorageConnectionString, configuration.StorageContainerName);
            return new CertificateIssuer(CreateKeyVaultClient(), configuration.RootCertificateId, serialNumberGenerator);
        }

        private static Configuration ReadConfiguration(string functionAppDirectory)
        {
            var configurationRoot = new ConfigurationBuilder()
                .SetBasePath(functionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            var configuration = configurationRoot.Get<Configuration>();
            return configuration;
        }

        private static KeyVaultClient CreateKeyVaultClient()
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            return new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback));
        }
    }
}
