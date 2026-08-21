using POS.Application.Commands;
using POS.Application.Interfaces.Infrastructure;

namespace POS.Infrastructure.Reports;

/// <summary>
/// Factory that provides the correct renderer based on the requested export format.
/// </summary>
public sealed class ReportRendererFactory : IReportRendererFactory
{
    private readonly QuestPdfReportRenderer _pdfRenderer;
    private readonly ExcelReportRenderer _excelRenderer;

    public ReportRendererFactory(QuestPdfReportRenderer pdfRenderer, ExcelReportRenderer excelRenderer)
    {
        _pdfRenderer = pdfRenderer;
        _excelRenderer = excelRenderer;
    }

    public IReportRenderer GetRenderer(ReportExportFormat format)
    {
        return format switch
        {
            ReportExportFormat.Pdf => _pdfRenderer,
            ReportExportFormat.Excel => _excelRenderer,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported export format.")
        };
    }
}
