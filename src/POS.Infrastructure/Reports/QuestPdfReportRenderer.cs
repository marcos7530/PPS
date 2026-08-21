using System.Globalization;
using POS.Application.Interfaces.Infrastructure;
using POS.Application.Views;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace POS.Infrastructure.Reports;

/// <summary>
/// Renders report data as an A4 PDF document using QuestPDF (Req 7.5).
/// Maximum 50,000 rows.
/// </summary>
public sealed class QuestPdfReportRenderer : IReportRenderer
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public string ContentType => "application/pdf";
    public string FileExtension => ".pdf";

    public Task<byte[]> RenderAsync(ReportRenderPayload payload, CancellationToken ct = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.MarginHorizontal(20);
                page.MarginVertical(15);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Element(header => RenderHeader(header, payload));
                page.Content().Element(content => RenderContent(content, payload));
                page.Footer().Element(RenderFooter);
            });
        });

        var bytes = document.GeneratePdf();
        return Task.FromResult(bytes);
    }

    private static void RenderHeader(IContainer container, ReportRenderPayload payload)
    {
        container.Column(col =>
        {
            col.Item().Text($"Sales Report: {payload.DateFrom:yyyy-MM-dd} to {payload.DateTo:yyyy-MM-dd}")
                .FontSize(14).Bold();

            col.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Text($"Total Sales: {payload.Summary.TotalSales.ToString("N2", Inv)}");
                row.RelativeItem().Text($"Transactions: {payload.Summary.TransactionCount}");
                row.RelativeItem().Text($"Average Value: {payload.Summary.AverageValue.ToString("N2", Inv)}");
                row.RelativeItem().Text($"Total Discounts: {payload.Summary.TotalDiscounts.ToString("N2", Inv)}");
                row.RelativeItem().Text($"Gross Margin: {payload.Summary.TotalGrossMargin.ToString("N2", Inv)}");
            });

            col.Item().PaddingTop(5).LineHorizontal(0.5f);
        });
    }

    private static void RenderContent(IContainer container, ReportRenderPayload payload)
    {
        container.Column(col =>
        {
            col.Spacing(5);

            // Category summaries section
            if (payload.CategorySummaries.Count > 0)
            {
                col.Item().PaddingTop(5).Text("Summary by Category").FontSize(10).Bold();
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3); // Category
                        columns.RelativeColumn(2); // Total Sales
                        columns.RelativeColumn(1); // Tx Count
                        columns.RelativeColumn(2); // Gross Margin
                        columns.RelativeColumn(2); // Avg Margin %
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Category").Bold();
                        header.Cell().Text("Total Sales").Bold();
                        header.Cell().Text("Transactions").Bold();
                        header.Cell().Text("Gross Margin").Bold();
                        header.Cell().Text("Margin %").Bold();
                    });

                    foreach (var cat in payload.CategorySummaries)
                    {
                        table.Cell().Text(cat.CategoryName);
                        table.Cell().Text(cat.TotalSales.ToString("N2", Inv));
                        table.Cell().Text(cat.TransactionCount.ToString(Inv));
                        table.Cell().Text(cat.TotalGrossMargin.ToString("N2", Inv));
                        table.Cell().Text(cat.AverageRealizedMarginPercentage.ToString("N2", Inv) + "%");
                    }
                });
            }

            // Line items detail section
            col.Item().PaddingTop(10).Text("Transaction Details").FontSize(10).Bold();
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1); // Tx#
                    columns.RelativeColumn(1.5f); // Date
                    columns.RelativeColumn(3); // Product
                    columns.RelativeColumn(2); // Category
                    columns.RelativeColumn(1); // Qty
                    columns.RelativeColumn(1.5f); // Unit Price
                    columns.RelativeColumn(1.5f); // Cost
                    columns.RelativeColumn(1.5f); // Discount
                    columns.RelativeColumn(1.5f); // Line Amount
                    columns.RelativeColumn(1.5f); // Gross Margin
                    columns.RelativeColumn(1.5f); // Margin %
                });

                table.Header(header =>
                {
                    header.Cell().Text("Tx #").Bold();
                    header.Cell().Text("Date").Bold();
                    header.Cell().Text("Product").Bold();
                    header.Cell().Text("Category").Bold();
                    header.Cell().Text("Qty").Bold();
                    header.Cell().Text("Unit Price").Bold();
                    header.Cell().Text("Cost").Bold();
                    header.Cell().Text("Discount").Bold();
                    header.Cell().Text("Line Amt").Bold();
                    header.Cell().Text("Gross Margin").Bold();
                    header.Cell().Text("Margin %").Bold();
                });

                foreach (var row in payload.Rows)
                {
                    table.Cell().Text(row.TransactionNumber.ToString(Inv));
                    table.Cell().Text(row.OperatingDay.ToString("yyyy-MM-dd", Inv));
                    table.Cell().Text(row.ProductName);
                    table.Cell().Text(row.CategoryName);
                    table.Cell().Text(row.Quantity.ToString(Inv));
                    table.Cell().Text(row.UnitPrice.ToString("N2", Inv));
                    table.Cell().Text(row.CostPrice.ToString("N2", Inv));
                    table.Cell().Text(row.LineDiscountAmount.ToString("N2", Inv));
                    table.Cell().Text(row.LineAmount.ToString("N2", Inv));
                    table.Cell().Text(row.GrossMargin.ToString("N2", Inv));
                    table.Cell().Text(row.RealizedMarginPercentage.ToString("N2", Inv) + "%");
                }
            });
        });
    }

    private static void RenderFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.Span("Page ");
            text.CurrentPageNumber();
            text.Span(" of ");
            text.TotalPages();
        });
    }
}
