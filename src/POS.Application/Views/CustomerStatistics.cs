namespace POS.Application.Views;

/// <summary>
/// Customer lifetime statistics: total transactions, total amount, last purchase (Req 13.14).
/// </summary>
public sealed record CustomerStatistics(
    Guid CustomerId,
    int TotalTransactions,
    decimal TotalPurchaseAmount,
    DateTimeOffset? LastPurchaseDate);
