using Microsoft.Extensions.Options;

namespace ClinicQ.Web.Infrastructure.Storage;

/// <summary>
/// Local-disk fallback used when no Azure Storage connection string is configured.
/// Files are stored under {contentRoot}/{LocalRootPath}/{container}/{yyyy}/{MM}/{guid}_{fileName}.
/// </summary>
public sealed class LocalDiskFileStorage : IFileStorage
{
    private readonly string _root;
    private readonly ILogger<LocalDiskFileStorage> _logger;

    public LocalDiskFileStorage(IOptions<StorageOptions> options, IHostEnvironment environment, ILogger<LocalDiskFileStorage> logger)
    {
        _root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.LocalRootPath));
        _logger = logger;
    }

    public string ProviderName => "LocalDisk";

    public async Task<StoredFile> SaveAsync(string container, string fileName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var safeName = Path.GetFileName(fileName);
        var relative = Path.Combine(container, DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"), $"{Guid.NewGuid():N}_{safeName}");
        var full = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        await using var target = File.Create(full);
        await content.CopyToAsync(target, cancellationToken);

        _logger.LogInformation("Stored {File} ({Bytes} bytes) on local disk", relative, target.Length);
        return new StoredFile(relative.Replace('\\', '/'), target.Length);
    }

    public Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var full = Resolve(path);
        Stream? stream = File.Exists(full) ? File.OpenRead(full) : null;
        return Task.FromResult(stream);
    }

    public Task<DownloadLink> GetDownloadLinkAsync(string path, TimeSpan validFor, string fallbackUrl, CancellationToken cancellationToken = default)
        => Task.FromResult(new DownloadLink(fallbackUrl, DateTimeOffset.UtcNow.Add(validFor)));

    public Task<bool> DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var full = Resolve(path);
        if (!File.Exists(full))
        {
            return Task.FromResult(false);
        }

        File.Delete(full);
        return Task.FromResult(true);
    }

    private string Resolve(string path)
    {
        var full = Path.GetFullPath(Path.Combine(_root, path));
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Storage path escapes the configured root.");
        }

        return full;
    }
}
