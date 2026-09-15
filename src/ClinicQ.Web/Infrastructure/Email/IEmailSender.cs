namespace ClinicQ.Web.Infrastructure.Email;

public sealed record EmailMessage(string To, string Subject, string HtmlBody, string? PlainTextBody = null);

/// <summary>
/// Outbound email abstraction. SendGrid in production, console logging when no API key is configured.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
