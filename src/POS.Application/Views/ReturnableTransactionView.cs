using POS.Domain.ValueObjects;

namespace POS.Application.Views;

/// <summary>
/// View of a transaction that can be returned (non-voided, within valid window).
/// </summary>
public sealed record ReturnableTransactionView(
    Guid TransactionId,
    long TransactionNumber,
    DateTimeOffset CompletedAt,
    IReadOnlyList<ReturnableLineItemView> Lines);

/// <summary>
/// View of a line item that is eligible for return.
/// </summary>
public sealed record ReturnableLineItemView(
    Guid LineItemId,
    Guid ProductId,
    string ProductName,
    int OriginalQuantity,
    int AlreadyReturnedQuantity,
    int ReturnableQuantity,
    Money UnitPrice);
