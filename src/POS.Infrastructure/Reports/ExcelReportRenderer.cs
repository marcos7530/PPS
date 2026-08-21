using System.Globalization;
using ClosedXML.Excel;
using POS.Application.Interfaces.Infrastructure;
using POS.Application.Views;

namespace POS.Infrastructure.Reports;

/// <summary>
/// Renders report data as an Excel (.xlsx) workbook using ClosedXML (Req 7.5).
/// Maximum 100,000 rows.
/// </summary>
public sealed class ExcelReportRenderer : IReportRenderer
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public string FileExtension => ".xlsx";

    public Task<byte[]> RenderAsync(ReportRenderPayload payload, CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook();

        // Summary sheet
        var summarySheet = workbook.Worksheets.Add("Summary");
        WriteSummary(summarySheet, payload);

        // Category summary sheet
        if (payload.CategorySummaries.Count > 0)
        {
            var categorySheet = workbook.Worksheets.Add("By Category");
            WriteCategorySummaries(categorySheet, payload.CategorySummaries);
        }

        // Product summary sheet
        if (payload.ProductSummaries.Count > 0)
        {
            var productSheet = workbook.Worksheets.Add("By Product");
            WriteProductSummaries(productSheet, payload.ProductSummaries);
        }

        // Detail sheet
        var detailSheet = workbook.Worksheets.Add("Details");
        WriteDetails(detailSheet, payload.Rows);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }

    private static void WriteSummary(IXLWorksheet ws, ReportRenderPayload payload)
    {
        ws.Cell(1, 1).Value = "Sales Report";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        ws.Cell(3, 1).Value = "Date From";
        ws.Cell(3, 2).Value = payload.DateFrom.ToString("yyyy-MM-dd", Inv);
        ws.Cell(4, 1).Value = "Date To";
        ws.Cell(4, 2).Value = payload.DateTo.ToString("yyyy-MM-dd", Inv);

        ws.Cell(6, 1).Value = "Total Sales";
        ws.Cell(6, 2).Value = payload.Summary.TotalSales;
        ws.Cell(6, 2).Style.NumberFormat.Format = "#,##0.00";

        ws.Cell(7, 1).Value = "Transaction Count";
        ws.Cell(7, 2).Value = payload.Summary.TransactionCount;

        ws.Cell(8, 1).Value = "Average Value";
        ws.Cell(8, 2).Value = payload.Summary.AverageValue;
        ws.Cell(8, 2).Style.NumberFormat.Format = "#,##0.00";

        ws.Cell(9, 1).Value = "Total Discounts";
        ws.Cell(9, 2).Value = payload.Summary.TotalDiscounts;
        ws.Cell(9, 2).Style.NumberFormat.Format = "#,##0.00";

        ws.Cell(10, 1).Value = "Total Gross Margin";
        ws.Cell(10, 2).Value = payload.Summary.TotalGrossMargin;
        ws.Cell(10, 2).Style.NumberFormat.Format = "#,##0.00";

        ws.Columns().AdjustToContents();
    }

    private static void WriteCategorySummaries(IXLWorksheet ws, IReadOnlyList<ReportCategorySummary> summaries)
    {
        // Header
        ws.Cell(1, 1).Value = "Category";
        ws.Cell(1, 2).Value = "Total Sales";
        ws.Cell(1, 3).Value = "Transactions";
        ws.Cell(1, 4).Value = "Gross Margin";
        ws.Cell(1, 5).Value = "Avg Margin %";

        var headerRange = ws.Range(1, 1, 1, 5);
        headerRange.Style.Font.Bold = true;

        int row = 2;
        foreach (var cat in summaries)
        {
            ws.Cell(row, 1).Value = cat.CategoryName;
            ws.Cell(row, 2).Value = cat.TotalSales;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 3).Value = cat.TransactionCount;
            ws.Cell(row, 4).Value = cat.TotalGrossMargin;
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 5).Value = cat.AverageRealizedMarginPercentage;
            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void WriteProductSummaries(IXLWorksheet ws, IReadOnlyList<ReportProductSummary> summaries)
    {
        // Header
        ws.Cell(1, 1).Value = "Product";
        ws.Cell(1, 2).Value = "Category";
        ws.Cell(1, 3).Value = "Qty Sold";
        ws.Cell(1, 4).Value = "Total Sales";
        ws.Cell(1, 5).Value = "Gross Margin";
        ws.Cell(1, 6).Value = "Avg Margin %";

        var headerRange = ws.Range(1, 1, 1, 6);
        headerRange.Style.Font.Bold = true;

        int row = 2;
        foreach (var prod in summaries)
        {
            ws.Cell(row, 1).Value = prod.ProductName;
            ws.Cell(row, 2).Value = prod.CategoryName;
            ws.Cell(row, 3).Value = prod.TotalQuantitySold;
            ws.Cell(row, 4).Value = prod.TotalSales;
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 5).Value = prod.TotalGrossMargin;
            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 6).Value = prod.AverageRealizedMarginPercentage;
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void WriteDetails(IXLWorksheet ws, IReadOnlyList<ReportLineItemRow> rows)
    {
        // Header
        ws.Cell(1, 1).Value = "Tx #";
        ws.Cell(1, 2).Value = "Date";
        ws.Cell(1, 3).Value = "Completed At";
        ws.Cell(1, 4).Value = "Product";
        ws.Cell(1, 5).Value = "Category";
        ws.Cell(1, 6).Value = "Cashier";
        ws.Cell(1, 7).Value = "Qty";
        ws.Cell(1, 8).Value = "Unit Price";
        ws.Cell(1, 9).Value = "Cost Price";
        ws.Cell(1, 10).Value = "Discount";
        ws.Cell(1, 11).Value = "Line Amount";
        ws.Cell(1, 12).Value = "Gross Margin";
        ws.Cell(1, 13).Value = "Margin %";

        var headerRange = ws.Range(1, 1, 1, 13);
        headerRange.Style.Font.Bold = true;

        int rowNum = 2;
        foreach (var row in rows)
        {
            ws.Cell(rowNum, 1).Value = row.TransactionNumber;
            ws.Cell(rowNum, 2).Value = row.OperatingDay.ToString("yyyy-MM-dd", Inv);
            ws.Cell(rowNum, 3).Value = row.CompletedAt.ToString("yyyy-MM-dd HH:mm:ss", Inv);
            ws.Cell(rowNum, 4).Value = row.ProductName;
            ws.Cell(rowNum, 5).Value = row.CategoryName;
            ws.Cell(rowNum, 6).Value = row.CashierName;
            ws.Cell(rowNum, 7).Value = row.Quantity;
            ws.Cell(rowNum, 8).Value = row.UnitPrice;
            ws.Cell(rowNum, 8).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(rowNum, 9).Value = row.CostPrice;
            ws.Cell(rowNum, 9).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(rowNum, 10).Value = row.LineDiscountAmount;
            ws.Cell(rowNum, 10).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(rowNum, 11).Value = row.LineAmount;
            ws.Cell(rowNum, 11).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(rowNum, 12).Value = row.GrossMargin;
            ws.Cell(rowNum, 12).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(rowNum, 13).Value = row.RealizedMarginPercentage;
            ws.Cell(rowNum, 13).Style.NumberFormat.Format = "#,##0.00";
            rowNum++;
        }

        ws.Columns().AdjustToContents();
    }
}
