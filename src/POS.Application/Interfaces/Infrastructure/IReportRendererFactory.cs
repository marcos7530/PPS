using POS.Application.Commands;

namespace POS.Application.Interfaces.Infrastructure;

/// <summary>
/// Factory port for obtaining the correct report renderer by export format.
/// </summary>
public interface IReportRendererFactory
{
    /// <summary>
    /// Returns the appropriate renderer for the specified export format.
    /// </summary>
    IReportRenderer GetRenderer(ReportExportFormat format);
}
