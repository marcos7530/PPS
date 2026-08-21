namespace POS.Application.Commands;

/// <summary>
/// Command to generate a sales/audit report with filtering parameters (Req 7.1).
/// </summary>
public sealed record GenerateReportCommand(
    DateOnly DateFrom,
    DateOnly DateTo,
    IReadOnlyList<Guid>? CategoryIds,
    IReadOnlyList<Guid>? UserIds,
    bool IncludeChildCategories,
    ReportExportFormat ExportFormat,
    Guid PerformedBy);

/// <summary>
/// Supported export formats for reports (Req 7.5).
/// </summary>
public enum ReportExportFormat
{
    Pdf,
    Excel
}
