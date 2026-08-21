namespace POS.Application.Commands;

/// <summary>
/// Command to create a new product (Req 10.1).
/// </summary>
public sealed record CreateProductCommand(
    string Name,
    string Sku,
    string? Description,
    decimal SalePrice,
    decimal CostPrice,
    Guid CategoryId,
    int Quantity,
    int MinStockThreshold);
