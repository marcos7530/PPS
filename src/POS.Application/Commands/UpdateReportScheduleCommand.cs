namespace POS.Application.Commands;

/// <summary>
/// Command to update an existing scheduled report configuration (Req 7.7).
/// </summary>
public sealed record UpdateReportScheduleCommand(
    Guid ScheduleId,
    Guid PerformedBy,
    string? Frequency,
    string? ExportFormat,
    IReadOnlyList<string>? Recipients,
    string? FilterJson,
    bool? IsActive);
