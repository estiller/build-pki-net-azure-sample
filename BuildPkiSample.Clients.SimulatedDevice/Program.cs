using System;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Configuration;

namespace BuildPkiSample.Clients.SimulatedDevice
{
    internal class Program
    {
        private const string GlobalDeviceEndpoint = "global.azure-devices-provisioning.net";

        static async Task Main()
        {
            var configuration = ReadConfiguration();

            using X509Certificate2 certificate = LoadCertificate(configuration.DeviceName);
            using var security = new SecurityProviderX509Certificate(certificate);
            var registrationResult = await RegisterDeviceAsync(configuration, security);

            var auth = new DeviceAuthenticationWithX509Certificate(registrationResult.DeviceId, security.GetAuthenticationCertificate());
            using DeviceClient iotClient = DeviceClient.Create(registrationResult.AssignedHub, auth, TransportType.Amqp);
            await iotClient.OpenAsync();
            await iotClient.SendEventAsync(new Message(Encoding.UTF8.GetBytes("TestMessage")));
            await iotClient.CloseAsync();
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

        private static X509Certificate2 LoadCertificate(string deviceName)
        {
            using var store = new X509Store(StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            var certificateCollection = store.Certificates.Find(X509FindType.FindBySubjectName, deviceName, false);

            if (certificateCollection.Count == 0)
            {
                throw new Exception($"No matching certificate found for subject '{deviceName}'");
            }

            return certificateCollection[0];
        }

        private static async Task<DeviceRegistrationResult> RegisterDeviceAsync(Configuration configuration, SecurityProviderX509Certificate security)
        {
            using var transport = new ProvisioningTransportHandlerAmqp(TransportFallbackType.TcpOnly);
            var provClient = ProvisioningDeviceClient.Create(GlobalDeviceEndpoint, configuration.DpsIdScope, security, transport);
            DeviceRegistrationResult registrationResult = await provClient.RegisterAsync();
            Console.WriteLine($"Registration {registrationResult.Status}");
            Console.WriteLine($"ProvisioningClient AssignedHub: {registrationResult.AssignedHub}; DeviceID: {registrationResult.DeviceId}");

            if (registrationResult.Status != ProvisioningRegistrationStatusType.Assigned) 
                throw new Exception("IoT Hub not assigned!");
            return registrationResult;
        }
    }
}
