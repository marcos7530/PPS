using POS.Domain.ValueObjects;

namespace POS.Application.Views;

/// <summary>
/// View of a completed sale transaction.
/// </summary>
public sealed record CompletedSale(
    Guid TransactionId,
    long TransactionNumber,
    Money FinalAmount,
    Money AmountReceived,
    Money ChangeDue,
    DateTimeOffset CompletedAt);
