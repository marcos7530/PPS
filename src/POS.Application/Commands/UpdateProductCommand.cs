namespace POS.Application.Commands;

/// <summary>
/// Command to update an existing product (Req 10.3).
/// </summary>
public sealed record UpdateProductCommand(
    Guid ProductId,
    string Name,
    string? Description,
    decimal SalePrice,
    decimal CostPrice,
    Guid CategoryId,
    int Quantity,
    int MinStockThreshold);
