using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using BuildPkiSample.CertificateAuthority.BusinessLogic;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace BuildPkiSample.CertificateAuthority.Api
{
    public static class RenewCertificateFunction
    {
        [FunctionName("RenewCertificateFunction")]
        public static async Task Run([ServiceBusTrigger("certificaterenewalrequests", Connection = "ServiceBusQueueConnection")]Message message, 
            ExecutionContext context, ILogger log)
        {
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {message.MessageId}");
            var certificateIssuer = CertificateFunctionHelper.CreateCertificateIssuer(context.FunctionAppDirectory);

            var (subjectName, publicKey) = ExtractData(message);
            var certificate = await certificateIssuer.IssueCertificateAsync(subjectName, publicKey);

            byte[] certificateBuffer = certificate.Export(X509ContentType.Cert);
            string encodedCertificate = Convert.ToBase64String(certificateBuffer);
            throw new NotImplementedException("Need to return data!");
        }

        private static (string subjectName, RSAPublicKeyParameters publicKey) ExtractData(Message message)
        {
            throw new NotImplementedException();
        }

    }
}
