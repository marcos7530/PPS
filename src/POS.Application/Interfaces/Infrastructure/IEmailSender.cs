namespace POS.Application.Interfaces.Infrastructure;

/// <summary>
/// Port for sending emails (password reset, reports, receipts).
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends an email to the specified recipients.
    /// </summary>
    Task SendAsync(
        string subject,
        string body,
        IReadOnlyList<string> recipients,
        IReadOnlyList<EmailAttachment>? attachments = null,
        CancellationToken ct = default);
}

/// <summary>
/// Represents an email attachment.
/// </summary>
public sealed record EmailAttachment(
    string FileName,
    string ContentType,
    byte[] Content);
