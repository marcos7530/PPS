namespace POS.Application.DTOs;

/// <summary>
/// Generated receipt document ready for output.
/// </summary>
public sealed record ReceiptDocument(
    Guid ReceiptId,
    byte[] Content,
    ReceiptChannel Channel,
    string ContentType);
