namespace POS.Application.DTOs;

/// <summary>
/// Sales by category metric for pie chart rendering (Req 8.1, 8.6).
/// </summary>
public sealed record CategoryMetric(
    string CategoryName,
    decimal TotalSales,
    decimal Percentage);
