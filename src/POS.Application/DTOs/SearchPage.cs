using POS.Domain.Entities;

namespace POS.Application.DTOs;

/// <summary>
/// Paginated search results for product name search.
/// </summary>
public sealed record SearchPage(
    IReadOnlyList<Product> Items,
    int TotalCount);
