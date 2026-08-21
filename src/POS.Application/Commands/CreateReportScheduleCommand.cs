namespace POS.Application.Commands;

/// <summary>
/// Command to create a scheduled report configuration (Req 7.7).
/// </summary>
public sealed record CreateReportScheduleCommand(
    Guid CreatedBy,
    string ReportType,
    string Frequency,
    string ExportFormat,
    IReadOnlyList<string> Recipients,
    string FilterJson);
