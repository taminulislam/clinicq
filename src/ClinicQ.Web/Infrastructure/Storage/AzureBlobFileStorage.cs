using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace ClinicQ.Web.Infrastructure.Storage;

/// <summary>
/// Azure Blob Storage implementation. Download links are user-delegation-free account SAS tokens
/// scoped to a single blob with read permission and a short expiry.
/// </summary>
public sealed class AzureBlobFileStorage : IFileStorage
{
    private readonly BlobServiceClient _client;
    private readonly StorageOptions _options;
    private readonly ILogger<AzureBlobFileStorage> _logger;

    public AzureBlobFileStorage(BlobServiceClient client, IOptions<StorageOptions> options, ILogger<AzureBlobFileStorage> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "AzureBlob";

    public async Task<StoredFile> SaveAsync(string container, string fileName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var containerClient = _client.GetBlobContainerClient(_options.ContainerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var blobName = $"{container}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";
        var blob = containerClient.GetBlobClient(blobName);
        await blob.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: cancellationToken);

        var props = await blob.GetPropertiesAsync(cancellationToken: cancellationToken);
        _logger.LogInformation("Uploaded {Blob} ({Bytes} bytes) to Azure Blob Storage", blobName, props.Value.ContentLength);
        return new StoredFile(blobName, props.Value.ContentLength);
    }

    public async Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var blob = _client.GetBlobContainerClient(_options.ContainerName).GetBlobClient(path);
        if (!await blob.ExistsAsync(cancellationToken))
        {
            return null;
        }

        return await blob.OpenReadAsync(cancellationToken: cancellationToken);
    }

    public Task<DownloadLink> GetDownloadLinkAsync(string path, TimeSpan validFor, string fallbackUrl, CancellationToken cancellationToken = default)
    {
        var blob = _client.GetBlobContainerClient(_options.ContainerName).GetBlobClient(path);
        var expires = DateTimeOffset.UtcNow.Add(validFor);

        if (!blob.CanGenerateSasUri)
        {
            // Managed identity without a delegation key cannot mint SAS tokens; fall back to the API stream endpoint.
            _logger.LogWarning("Blob client cannot generate SAS; falling back to API download endpoint");
            return Task.FromResult(new DownloadLink(fallbackUrl, expires));
        }

        var sas = new BlobSasBuilder(BlobSasPermissions.Read, expires)
        {
            BlobContainerName = _options.ContainerName,
            BlobName = path,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-2)
        };

        var uri = blob.GenerateSasUri(sas);
        return Task.FromResult(new DownloadLink(uri.ToString(), expires));
    }

    public async Task<bool> DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var blob = _client.GetBlobContainerClient(_options.ContainerName).GetBlobClient(path);
        var response = await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        return response.Value;
    }
}
