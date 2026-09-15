namespace ClinicQ.Web.Infrastructure.Email;

/// <summary>
/// Development fallback: writes the message to the log instead of sending it.
/// </summary>
public sealed class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[EMAIL] To={To} Subject={Subject} Body={Body}",
            message.To, message.Subject, message.PlainTextBody ?? message.HtmlBody);
        return Task.CompletedTask;
    }
}
