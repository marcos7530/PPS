using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using POS.Application.Interfaces.Infrastructure;

namespace POS.Infrastructure.Email;

/// <summary>
/// Sends emails via MailKit/SMTP with 3 retry attempts and exponential backoff (Req 4.4, 7.8, 17.6).
/// </summary>
public sealed partial class MailKitEmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(IOptions<SmtpSettings> settings, ILogger<MailKitEmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task SendAsync(
        string subject,
        string body,
        IReadOnlyList<string> recipients,
        IReadOnlyList<EmailAttachment>? attachments = null,
        CancellationToken ct = default)
    {
        var message = BuildMessage(subject, body, recipients, attachments);
        var recipientList = string.Join(", ", recipients);

        const int maxRetries = 3;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await SendViaSmtpAsync(message, ct);
                LogEmailSent(_logger, recipientList, attempt);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                LogEmailRetry(_logger, ex, attempt, recipientList);
                // Exponential backoff: 1s, 2s, 4s
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), ct);
            }
        }

        // Final attempt - let exception propagate
        await SendViaSmtpAsync(message, ct);
    }

    private MimeMessage BuildMessage(
        string subject,
        string body,
        IReadOnlyList<string> recipients,
        IReadOnlyList<EmailAttachment>? attachments)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));

        foreach (var recipient in recipients)
        {
            message.To.Add(MailboxAddress.Parse(recipient));
        }

        message.Subject = subject;

        var builder = new BodyBuilder { TextBody = body };

        if (attachments is not null)
        {
            foreach (var attachment in attachments)
            {
                builder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
            }
        }

        message.Body = builder.ToMessageBody();
        return message;
    }

    private async Task SendViaSmtpAsync(MimeMessage message, CancellationToken ct)
    {
        using var client = new SmtpClient();

        await client.ConnectAsync(
            _settings.Host,
            _settings.Port,
            _settings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls,
            ct);

        if (!string.IsNullOrEmpty(_settings.Username) && !string.IsNullOrEmpty(_settings.Password))
        {
            await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);
        }

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email sent successfully to {Recipients} on attempt {Attempt}")]
    private static partial void LogEmailSent(ILogger logger, string recipients, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Email send attempt {Attempt} failed for {Recipients}. Retrying...")]
    private static partial void LogEmailRetry(ILogger logger, Exception ex, int attempt, string recipients);
}

/// <summary>
/// SMTP configuration settings for MailKit.
/// </summary>
public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string SenderName { get; set; } = "POS System";
    public string SenderEmail { get; set; } = "noreply@pos.local";
}
