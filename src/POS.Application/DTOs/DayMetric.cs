namespace POS.Application.DTOs;

/// <summary>
/// Daily sales metric for line chart rendering (Req 8.1, 8.6).
/// </summary>
public sealed record DayMetric(
    DateOnly Day,
    decimal Amount,
    int TxCount);
