namespace POS.Application.DTOs;

/// <summary>
/// Top product metric for bar chart rendering (Req 8.1).
/// </summary>
public sealed record ProductMetric(
    string ProductName,
    int QuantitySold,
    decimal TotalSales);
