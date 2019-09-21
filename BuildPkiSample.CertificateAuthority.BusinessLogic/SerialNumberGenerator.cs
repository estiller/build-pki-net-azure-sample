using System;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Polly;

namespace BuildPkiSample.CertificateAuthority.BusinessLogic
{
    public class SerialNumberGenerator
    {
        private const string BlobName = "serial";
        private const int SerialByteMaxLength = 20;
        private const int PreconditionFailedRetryAmount = 10;
        private readonly CloudBlobContainer _container;

        public SerialNumberGenerator(string storageConnectionString, string containerName)
        {
            _container = CloudStorageAccount.Parse(storageConnectionString).CreateCloudBlobClient().GetContainerReference(containerName);
        }

        public Task<byte[]> GenerateSerialAsync()
        {
            return Policy<byte[]>
                .Handle((StorageException e) =>
                    e.RequestInformation.HttpStatusCode == (int) HttpStatusCode.PreconditionFailed)
                .RetryAsync(PreconditionFailedRetryAmount)
                .ExecuteAsync(GenerateSerialInternalAsync);
        }

        private async Task<byte[]> GenerateSerialInternalAsync()
        {
            CloudBlockBlob? blob = await GetBlobAsync();
            if (blob == null)
            {
                byte[] zero = new byte[1];
                await CreateNewBlobAsync(zero);
                return zero;
            }

            byte[] serialBuffer = new byte[SerialByteMaxLength];
            int bytesRead = await blob.DownloadToByteArrayAsync(serialBuffer, 0,
                AccessCondition.GenerateIfMatchCondition(blob.Properties.ETag), null, null);
            Increment(serialBuffer, bytesRead);
            await blob.UploadFromByteArrayAsync(serialBuffer, 0, serialBuffer.Length,
                AccessCondition.GenerateIfMatchCondition(blob.Properties.ETag), null, null);
            return serialBuffer;
        }

        private async Task<CloudBlockBlob?> GetBlobAsync()
        {
            try
            {
                var blob = _container.GetBlockBlobReference(BlobName);
                await blob.FetchAttributesAsync();
                return blob;
            }
            catch (StorageException e) when (e.RequestInformation.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        private async Task CreateNewBlobAsync(byte[] serial)
        {
            await _container.CreateIfNotExistsAsync();
            var blob = _container.GetBlockBlobReference(BlobName);
            await blob.UploadFromByteArrayAsync(serial, 0, serial.Length, AccessCondition.GenerateIfNotExistsCondition(), null, null);
        }

        private static void Increment(byte[] serialBuffer, int length)
        {
            var serial = new BigInteger(new ReadOnlySpan<byte>(serialBuffer, 0, length), true, true);
            serial++;
            int byteCount = serial.GetByteCount(true);
            serial.TryWriteBytes(new Span<byte>(serialBuffer, serialBuffer.Length - byteCount, byteCount), out _, true, true);
        }

    }
}