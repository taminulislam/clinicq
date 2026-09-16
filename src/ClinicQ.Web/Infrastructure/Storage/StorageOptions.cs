namespace ClinicQ.Web.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Azure Storage connection string. Empty means local disk. Prefer a Key Vault reference in Azure.</summary>
    public string? AzureBlobConnectionString { get; set; }
    public string ContainerName { get; set; } = "lab-reports";
    /// <summary>Root folder for the local-disk fallback (relative to the content root).</summary>
    public string LocalRootPath { get; set; } = "storage";
    public int DownloadLinkMinutes { get; set; } = 15;
    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Accepted upload content types. Left empty by default: the configuration binder appends to a
    /// pre-populated array rather than replacing it, which would duplicate the defaults.
    /// </summary>
    public string[] AllowedContentTypes { get; set; } = Array.Empty<string>();

    public static readonly string[] DefaultContentTypes = { "application/pdf", "image/png", "image/jpeg" };

    /// <summary>Configured types when supplied, otherwise <see cref="DefaultContentTypes"/>.</summary>
    public IReadOnlyList<string> EffectiveContentTypes =>
        AllowedContentTypes.Length > 0 ? AllowedContentTypes : DefaultContentTypes;

    public bool UseAzureBlob => !string.IsNullOrWhiteSpace(AzureBlobConnectionString);
}
