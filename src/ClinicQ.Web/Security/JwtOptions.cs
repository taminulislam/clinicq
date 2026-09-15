namespace ClinicQ.Web.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "ClinicQ";
    public string Audience { get; set; } = "ClinicQ.Api";

    /// <summary>
    /// HMAC signing key (at least 32 characters). Leave empty in appsettings.json; supply from Key Vault
    /// (Jwt--SigningKey) in Azure. When empty, a random per-process key is generated for local development.
    /// </summary>
    public string? SigningKey { get; set; }

    public int ExpiryMinutes { get; set; } = 60;
}
