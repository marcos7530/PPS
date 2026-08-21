using POS.Domain.ValueObjects;

namespace POS.Application.Views;

/// <summary>
/// View of an in-progress transaction with computed totals.
/// </summary>
public sealed record OpenTransactionView(
    Guid TransactionId,
    IReadOnlyList<LineItemView> LineItems,
    Money Subtotal,
    Money TaxAmount,
    Money DiscountAmount,
    Money Total);

/// <summary>
/// View of a single line item in a transaction.
/// </summary>
public sealed record LineItemView(
    Guid LineItemId,
    Guid ProductId,
    string ProductName,
    string? Sku,
    int Quantity,
    Money UnitPrice,
    Money LineTotal,
    Money DiscountAmount);
