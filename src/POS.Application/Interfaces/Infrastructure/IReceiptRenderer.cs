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
/// Data payload for receipt rendering (Req 17.1-17.17).
/// Supports both transaction and return receipts, reprints, and voided transactions.
/// </summary>
public sealed record ReceiptPayload(
    string BusinessName,
    string BusinessAddress,
    long TransactionNumber,
    DateTimeOffset CompletedAt,
    string CashierName,
    string? CustomerName,
    IReadOnlyList<ReceiptLinePayload> Lines,
    decimal Subtotal,
    decimal TaxAmount,
    decimal DiscountAmount,
    decimal Total,
    decimal AmountReceived,
    decimal ChangeDue,
    string PaymentMethod,
    string? FooterText,
    // Store credit details (Req 17.2)
    decimal? StoreCreditAmount,
    string? VoucherCodeLast4,
    // Reprint info (Req 17.9)
    string? ReprintText,
    // Voided flag (Req 17.10)
    string? VoidedText,
    // Return receipt fields (Req 17.11)
    bool IsReturn,
    Guid? ReturnId,
    Guid? OriginalTransactionId,
    string? RefundMethod,
    string? StoreCreditVoucherCode);

/// <summary>
/// Line item data for receipt rendering.
/// </summary>
public sealed record ReceiptLinePayload(
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    decimal DiscountAmount);
