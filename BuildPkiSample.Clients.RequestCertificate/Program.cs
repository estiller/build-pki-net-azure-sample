using System;
using System.Net.Http;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace BuildPkiSample.Clients.RequestCertificate
{
    internal class Program
    {
        internal static async Task Main()
        {
            SetDefaultSerializerSettings();
            var configuration = ReadConfiguration();
            var accessToken = await GetAccessToken(configuration);
            var subjectName = ReadSubjectName();

            var key = RSA.Create();
            var publicParameters = key.ExportParameters(false);
            var certificate = await IssueCertificate(subjectName, publicParameters, configuration, accessToken);
            var certificateWithPrivateKey = certificate.CopyWithPrivateKey(key);
            StoreCertificate(certificateWithPrivateKey);
            
            Console.WriteLine("Stored issued certificate in the certificate store:");
            Console.WriteLine(certificateWithPrivateKey);
        }

        private static void SetDefaultSerializerSettings()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }

        private static Configuration ReadConfiguration()
        {
            var configurationRoot = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddUserSecrets<Program>()
                .Build();
            var configuration = configurationRoot.Get<Configuration>();
            return configuration;
        }

        private static async Task<string> GetAccessToken(Configuration configuration)
        {
            var authHelper = new AuthenticationHelper(configuration.ClientId, configuration.TenantId, configuration.CertificateAuthorityScope);
            var auth = await authHelper.AcquireTokenAsync();

            var client = new HttpClient { BaseAddress = new Uri(configuration.BaseUrl) };
            var request = new {access_token = auth.AccessToken};
            var httpContent = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, MediaTypeNames.Application.Json);
            var responseMessage = await client.PostAsync(".auth/login/aad", httpContent);
            responseMessage.EnsureSuccessStatusCode();
            var serializedResponse = await responseMessage.Content.ReadAsStringAsync();
            dynamic response = JsonConvert.DeserializeObject<dynamic>(serializedResponse);
            return response.authenticationToken;
        }

        private static string ReadSubjectName()
        {
            Console.Write("Enter certificate subject name: ");
            var subjectName = Console.ReadLine();
            return subjectName;
        }

        private static async Task<X509Certificate2> IssueCertificate(string subjectName, RSAParameters publicParameters,
            Configuration configuration, string accessToken)
        {
            var request = new IssueCertificateRequest(subjectName, publicParameters);
            var httpContent = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8,
                MediaTypeNames.Application.Json);

            var client = new HttpClient {BaseAddress = new Uri(configuration.BaseUrl)};
            client.DefaultRequestHeaders.Add("X-ZUMO-AUTH", accessToken);
            var responseMessage = await client.PostAsync("api/issueCertificate", httpContent);
            responseMessage.EnsureSuccessStatusCode();
            var serializedResponse = await responseMessage.Content.ReadAsStringAsync();

            var response = JsonConvert.DeserializeObject<IssueCertificateResponse>(serializedResponse);
            if (response.Certificate == null)
            {
                throw new Exception("Unexpected response");
            }

            var certificate = new X509Certificate2(Convert.FromBase64String(response.Certificate));
            return certificate;
        }

        private static void StoreCertificate(X509Certificate2 certificateWithPrivateKey)
        {
            using var store = new X509Store(StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            store.Add(certificateWithPrivateKey);
        }
    }
}
