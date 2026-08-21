namespace POS.Application.DTOs;

/// <summary>
/// Represents a stock quantity change for a product.
/// Positive delta = restock, negative delta = decrement.
/// </summary>
public sealed record StockDelta(Guid ProductId, int QuantityDelta);
