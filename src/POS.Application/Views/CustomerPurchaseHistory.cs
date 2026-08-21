namespace POS.Application.Views;

/// <summary>
/// A single purchase history entry for a customer (Req 13.9).
/// Shows the last 100 transactions.
/// </summary>
public sealed record CustomerPurchaseHistory(
    Guid TransactionId,
    long TransactionNumber,
    DateTimeOffset CompletedAt,
    decimal FinalAmount,
    string ProductSummary);
