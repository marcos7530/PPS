using POS.Application.Views;

namespace POS.Application.Interfaces.Infrastructure;

/// <summary>
/// Port for rendering report data into a specific file format (PDF or Excel).
/// </summary>
public interface IReportRenderer
{
    /// <summary>
    /// Renders the report data rows and summary into a byte array.
    /// </summary>
    Task<byte[]> RenderAsync(ReportRenderPayload payload, CancellationToken ct = default);

    /// <summary>
    /// The content type produced by this renderer (e.g., application/pdf).
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// The file extension for the output (e.g., .pdf, .xlsx).
    /// </summary>
    string FileExtension { get; }
}

/// <summary>
/// Payload for rendering a report, containing all data rows and summary.
/// </summary>
public sealed record ReportRenderPayload(
    DateOnly DateFrom,
    DateOnly DateTo,
    IReadOnlyList<ReportLineItemRow> Rows,
    ReportSummary Summary,
    IReadOnlyList<ReportCategorySummary> CategorySummaries,
    IReadOnlyList<ReportProductSummary> ProductSummaries);

/// <summary>
/// A single line-item row in the report data set.
/// </summary>
public sealed record ReportLineItemRow(
    long TransactionNumber,
    DateOnly OperatingDay,
    DateTimeOffset CompletedAt,
    string ProductName,
    string CategoryName,
    string CashierName,
    int Quantity,
    decimal UnitPrice,
    decimal CostPrice,
    decimal LineDiscountAmount,
    decimal LineAmount,
    decimal GrossMargin,
    decimal RealizedMarginPercentage);

/// <summary>
/// Aggregated summary per category.
/// </summary>
public sealed record ReportCategorySummary(
    string CategoryName,
    decimal TotalSales,
    int TransactionCount,
    decimal TotalGrossMargin,
    decimal AverageRealizedMarginPercentage);

/// <summary>
/// Aggregated summary per product.
/// </summary>
public sealed record ReportProductSummary(
    string ProductName,
    string CategoryName,
    int TotalQuantitySold,
    decimal TotalSales,
    decimal TotalGrossMargin,
    decimal AverageRealizedMarginPercentage);
