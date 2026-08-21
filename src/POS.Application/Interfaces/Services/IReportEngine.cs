using POS.Application.Commands;
using POS.Application.Views;
using POS.Domain.Common;

namespace POS.Application.Interfaces.Services;

/// <summary>
/// Service port for generating sales and audit reports (Req 7.1-7.6, 7.10).
/// </summary>
public interface IReportEngine
{
    /// <summary>
    /// Generates a report with the specified parameters.
    /// Returns the rendered file content with summary statistics.
    /// </summary>
    Task<Result<ReportResult>> GenerateAsync(GenerateReportCommand cmd, CancellationToken ct = default);
}
