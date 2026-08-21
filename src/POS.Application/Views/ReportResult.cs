namespace POS.Application.Views;

/// <summary>
/// Result of a generated report including content, metadata, and summary (Req 7.5, 7.6).
/// </summary>
public sealed record ReportResult(
    byte[] Content,
    string ContentType,
    string FileName,
    ReportSummary Summary,
    bool DataTruncated,
    string? TruncationMessage);

/// <summary>
/// Summary statistics for a generated report (Req 7.6, 15.22-15.25, 19.19).
/// </summary>
public sealed record ReportSummary(
    decimal TotalSales,
    int TransactionCount,
    decimal AverageValue,
    decimal TotalDiscounts,
    decimal TotalGrossMargin);
