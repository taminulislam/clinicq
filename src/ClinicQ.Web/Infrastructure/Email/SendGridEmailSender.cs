using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace ClinicQ.Web.Infrastructure.Email;

/// <summary>
/// Production email sender backed by SendGrid. Registered only when SendGrid:ApiKey is configured.
/// </summary>
public sealed class SendGridEmailSender : IEmailSender
{
    private readonly ISendGridClient _client;
    private readonly SendGridOptions _options;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(ISendGridClient client, IOptions<SendGridOptions> options, ILogger<SendGridEmailSender> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var mail = MailHelper.CreateSingleEmail(
            new EmailAddress(_options.FromEmail, _options.FromName),
            new EmailAddress(message.To),
            message.Subject,
            message.PlainTextBody ?? string.Empty,
            message.HtmlBody);

        var response = await _client.SendEmailAsync(mail, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Body.ReadAsStringAsync(cancellationToken);
            _logger.LogError("SendGrid rejected message to {To}: {Status} {Body}", message.To, response.StatusCode, body);
            throw new InvalidOperationException($"SendGrid returned {response.StatusCode}.");
        }

        _logger.LogInformation("Email sent to {To} via SendGrid ({Subject})", message.To, message.Subject);
    }
}
