namespace POS.Application.DTOs;

/// <summary>
/// Request to consume store credit during a sale.
/// </summary>
public sealed record StoreCreditRequest(
    Guid TransactionId,
    Guid CustomerId,
    string? VoucherCode);
