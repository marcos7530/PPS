namespace POS.Application.Commands;

/// <summary>
/// Command to create a new product category.
/// </summary>
public sealed record CreateCategoryCommand(
    string Name,
    Guid? ParentCategoryId,
    string? Description,
    int DisplayOrder,
    decimal? ProfitMarginPercentage);
