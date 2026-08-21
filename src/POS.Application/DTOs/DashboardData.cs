namespace POS.Application.DTOs;

/// <summary>
/// Complete dashboard metrics payload for all widgets (Req 8.1, 8.5, 8.10).
/// </summary>
public sealed record DashboardData(
    IReadOnlyList<DayMetric> SalesByDay,
    IReadOnlyList<ProductMetric> TopProducts,
    IReadOnlyList<CategoryMetric> SalesByCategory,
    decimal TotalSales,
    int TransactionCount,
    bool HasData,
    string? ErrorMessage);
