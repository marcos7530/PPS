namespace POS.Application.Views;

/// <summary>
/// View of a voided transaction.
/// </summary>
public sealed record VoidedTransactionView(
    Guid TransactionId,
    long TransactionNumber,
    string VoidReason,
    DateTimeOffset VoidedAt);
