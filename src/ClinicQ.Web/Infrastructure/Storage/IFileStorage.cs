namespace ClinicQ.Web.Infrastructure.Storage;

public sealed record StoredFile(string Path, long SizeBytes);

/// <summary>
/// A time-limited link a client can use to download a stored file.
/// For Azure Blob this is a SAS URL; for local disk it points at the API download endpoint.
/// </summary>
public sealed record DownloadLink(string Url, DateTimeOffset ExpiresAt);

/// <summary>
/// Binary storage for lab reports. Azure Blob Storage in production, local disk when no connection string is set.
/// </summary>
public interface IFileStorage
{
    string ProviderName { get; }
    Task<StoredFile> SaveAsync(string container, string fileName, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default);
    Task<DownloadLink> GetDownloadLinkAsync(string path, TimeSpan validFor, string fallbackUrl, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string path, CancellationToken cancellationToken = default);
}
