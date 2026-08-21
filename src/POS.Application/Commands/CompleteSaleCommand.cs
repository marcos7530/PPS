namespace POS.Application.Commands;

/// <summary>
/// Command to complete a sale transaction with payment details.
/// </summary>
public sealed record CompleteSaleCommand(
    Guid TransactionId,
    Guid UserId,
    Guid? CustomerId,
    IReadOnlyList<PaymentDetail> Payments);

/// <summary>
/// Payment method and amount for a sale.
/// </summary>
public sealed record PaymentDetail(
    string Method,
    decimal Amount,
    string? VoucherCode);
