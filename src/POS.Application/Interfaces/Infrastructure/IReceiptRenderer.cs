using POS.Application.DTOs;

namespace POS.Application.Interfaces.Infrastructure;

/// <summary>
/// Port for rendering receipt content to byte arrays (thermal/PDF formats).
/// </summary>
public interface IReceiptRenderer
{
    /// <summary>
    /// Renders a receipt to a byte array suitable for the specified channel.
    /// Thermal: ESC/POS commands for 80mm paper.
    /// PDF: A4 or 80mm page PDF document.
    /// </summary>
    Task<byte[]> RenderAsync(ReceiptPayload payload, ReceiptChannel channel, CancellationToken ct);
}

/// <summary>
/// Data payload for receipt rendering.
/// </summary>
public sealed record ReceiptPayload(
    string BusinessName,
    string BusinessAddress,
    long TransactionNumber,
    DateTimeOffset CompletedAt,
    string CashierName,
    IReadOnlyList<ReceiptLinePayload> Lines,
    decimal Subtotal,
    decimal TaxAmount,
    decimal DiscountAmount,
    decimal Total,
    decimal AmountReceived,
    decimal ChangeDue,
    string PaymentMethod,
    string? FooterText);

/// <summary>
/// Line item data for receipt rendering.
/// </summary>
public sealed record ReceiptLinePayload(
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    decimal DiscountAmount);
