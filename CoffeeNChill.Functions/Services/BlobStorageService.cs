using Azure.Storage.Blobs;

namespace CoffeeNChill.Functions.Services;

public class BlobStorageService
{
    private readonly BlobContainerClient _containerClient;

    public BlobStorageService()
    {
        string connectionString =
            Environment.GetEnvironmentVariable("AzureWebJobsStorage")
            ?? throw new InvalidOperationException(
                "AzureWebJobsStorage is not configured.");

        BlobServiceClient blobServiceClient =
            new BlobServiceClient(connectionString);

        _containerClient =
            blobServiceClient.GetBlobContainerClient("staff-docs");

        _containerClient.CreateIfNotExists();
    }

    public BlobContainerClient GetContainerClient()
    {
        return _containerClient;
    }
}