namespace ClinicQ.Web.Infrastructure.Email;

public sealed class SendGridOptions
{
    public const string SectionName = "SendGrid";

    /// <summary>API key. Leave empty to use the console fallback; in Azure supply it via Key Vault reference.</summary>
    public string? ApiKey { get; set; }
    public string FromEmail { get; set; } = "no-reply@clinicq.example";
    public string FromName { get; set; } = "ClinicQ";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
