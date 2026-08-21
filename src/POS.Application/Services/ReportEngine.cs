using POS.Application.Commands;
using POS.Application.Interfaces.Infrastructure;
using POS.Application.Interfaces.Repositories;
using POS.Application.Interfaces.Services;
using POS.Application.Views;
using POS.Domain.Common;

namespace POS.Application.Services;

/// <summary>
/// Generates sales reports with margin calculations, filtering, and export (Req 7.1-7.6, 7.10, 15.22-15.25, 19.19-19.20, 20.14).
/// </summary>
public sealed class ReportEngine : IReportEngine
{
    private const int MaxDateRangeDays = 366;
    private const int MaxPdfRows = 50_000;
    private const int MaxExcelRows = 100_000;

    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryClosureRepository _categoryClosureRepository;
    private readonly IReportRenderer _pdfRenderer;
    private readonly IReportRenderer _excelRenderer;

    public ReportEngine(
        ITransactionRepository transactionRepository,
        ICategoryClosureRepository categoryClosureRepository,
        IReportRendererFactory rendererFactory)
    {
        _transactionRepository = transactionRepository;
        _categoryClosureRepository = categoryClosureRepository;
        _pdfRenderer = rendererFactory.GetRenderer(ReportExportFormat.Pdf);
        _excelRenderer = rendererFactory.GetRenderer(ReportExportFormat.Excel);
    }

    public async Task<Result<ReportResult>> GenerateAsync(GenerateReportCommand cmd, CancellationToken ct = default)
    {
        // Validate date range (Req 7.2)
        var daySpan = cmd.DateTo.DayNumber - cmd.DateFrom.DayNumber + 1;
        if (daySpan < 1 || daySpan > MaxDateRangeDays)
            return Result<ReportResult>.Failure(ErrorCode.DateRangeExceedsLimit);

        try
        {
            // Resolve category filter including child categories if requested
            var categoryFilter = await ResolveCategoryFilterAsync(cmd.CategoryIds, cmd.IncludeChildCategories, ct);

            // Retrieve non-voided transactions for the date range (Req 20.14)
            var transactions = await GetFilteredTransactionsAsync(cmd.DateFrom, cmd.DateTo, categoryFilter, cmd.UserIds, ct);

            // Handle empty results (Req 7.10)
            if (transactions.Count == 0)
                return Result<ReportResult>.Failure(ErrorCode.NoDataFound);

            // Build line-item rows with margin calculations (Req 15.22-15.25)
            var rows = BuildLineItemRows(transactions);

            // Calculate summary statistics (Req 7.6, 19.19-19.20)
            var summary = CalculateSummary(transactions, rows);

            // Aggregate by category and product
            var categorySummaries = AggregateByCategoryName(rows);
            var productSummaries = AggregateByProduct(rows);

            // Determine row limit and check truncation (Req 7.5)
            var renderer = cmd.ExportFormat == ReportExportFormat.Pdf ? _pdfRenderer : _excelRenderer;
            var maxRows = cmd.ExportFormat == ReportExportFormat.Pdf ? MaxPdfRows : MaxExcelRows;

            bool truncated = rows.Count > maxRows;
            string? truncationMessage = truncated
                ? $"Report data has been limited to {maxRows:N0} rows. Total rows available: {rows.Count:N0}."
                : null;

            var outputRows = truncated ? rows.Take(maxRows).ToList() : rows;

            var payload = new ReportRenderPayload(
                cmd.DateFrom,
                cmd.DateTo,
                outputRows,
                summary,
                categorySummaries,
                productSummaries);

            var content = await renderer.RenderAsync(payload, ct);

            var fileName = $"Report_{cmd.DateFrom:yyyyMMdd}_{cmd.DateTo:yyyyMMdd}{renderer.FileExtension}";

            var result = new ReportResult(
                content,
                renderer.ContentType,
                fileName,
                summary,
                truncated,
                truncationMessage);

            return Result<ReportResult>.Success(result);
        }
        catch (Exception)
        {
            // Req 7.4: system error → generic failure message
            return Result<ReportResult>.Failure(ErrorCode.ReportGenerationFailed);
        }
    }

    private async Task<HashSet<Guid>?> ResolveCategoryFilterAsync(
        IReadOnlyList<Guid>? categoryIds, bool includeChildren, CancellationToken ct)
    {
        if (categoryIds is null || categoryIds.Count == 0)
            return null;

        var resolvedIds = new HashSet<Guid>(categoryIds);

        if (includeChildren)
        {
            foreach (var catId in categoryIds)
            {
                var descendants = await _categoryClosureRepository.GetDescendantIdsAsync(catId, ct);
                foreach (var descendantId in descendants)
                    resolvedIds.Add(descendantId);
            }
        }

        return resolvedIds;
    }

    private async Task<IReadOnlyList<Domain.Entities.Transaction>> GetFilteredTransactionsAsync(
        DateOnly dateFrom, DateOnly dateTo, HashSet<Guid>? categoryFilter, IReadOnlyList<Guid>? userIds, CancellationToken ct)
    {
        var allTransactions = new List<Domain.Entities.Transaction>();

        // Retrieve by operating day range
        for (var day = dateFrom; day <= dateTo; day = day.AddDays(1))
        {
            var dayTransactions = await _transactionRepository.GetByOperatingDayAsync(day, ct);
            allTransactions.AddRange(dayTransactions);
        }

        // Filter out voided transactions (Req 20.14)
        var filtered = allTransactions.Where(t => !t.IsVoided);

        // Filter by user
        if (userIds is not null && userIds.Count > 0)
        {
            var userSet = new HashSet<Guid>(userIds);
            filtered = filtered.Where(t => userSet.Contains(t.UserId));
        }

        // Filter by category (through line items)
        if (categoryFilter is not null)
        {
            filtered = filtered.Where(t =>
                t.LineItems.Any(li => categoryFilter.Contains(li.Product.CategoryId)));
        }

        return filtered.ToList();
    }

    private static List<ReportLineItemRow> BuildLineItemRows(IReadOnlyList<Domain.Entities.Transaction> transactions)
    {
        var rows = new List<ReportLineItemRow>();

        foreach (var tx in transactions)
        {
            foreach (var li in tx.LineItems)
            {
                var costPrice = li.RecordedCostPrice ?? 0m;
                var unitPrice = li.UnitPrice;
                var quantity = li.Quantity;

                // Req 15.22: Gross_Margin = (unit_price - cost_price) * quantity
                var grossMargin = Math.Round((unitPrice - costPrice) * quantity, 2, MidpointRounding.AwayFromZero);

                // Req 15.23: Realized_Margin_Percentage = (unit_price - cost_price) / unit_price * 100
                var realizedMarginPct = unitPrice > 0
                    ? Math.Round((unitPrice - costPrice) / unitPrice * 100m, 2, MidpointRounding.AwayFromZero)
                    : 0m;

                rows.Add(new ReportLineItemRow(
                    TransactionNumber: tx.TransactionNumber,
                    OperatingDay: tx.OperatingDay,
                    CompletedAt: tx.CompletedAt,
                    ProductName: li.ProductNameSnapshot,
                    CategoryName: li.Product?.Category?.Name ?? "Unknown",
                    CashierName: tx.User?.Username ?? "Unknown",
                    Quantity: quantity,
                    UnitPrice: unitPrice,
                    CostPrice: costPrice,
                    LineDiscountAmount: li.LineDiscountAmount,
                    LineAmount: li.LineAmount,
                    GrossMargin: grossMargin,
                    RealizedMarginPercentage: realizedMarginPct));
            }
        }

        return rows;
    }

    private static ReportSummary CalculateSummary(
        IReadOnlyList<Domain.Entities.Transaction> transactions,
        IReadOnlyList<ReportLineItemRow> rows)
    {
        // Req 7.6: total sales (2 dec), transaction count, average value (2 dec)
        var totalSales = Math.Round(transactions.Sum(t => t.FinalAmount), 2, MidpointRounding.AwayFromZero);
        var transactionCount = transactions.Count;
        var averageValue = transactionCount > 0
            ? Math.Round(totalSales / transactionCount, 2, MidpointRounding.AwayFromZero)
            : 0m;

        // Req 19.19-19.20: include discount totals
        var totalDiscounts = Math.Round(transactions.Sum(t => t.DiscountAmount), 2, MidpointRounding.AwayFromZero);

        // Total gross margin from all line items
        var totalGrossMargin = Math.Round(rows.Sum(r => r.GrossMargin), 2, MidpointRounding.AwayFromZero);

        return new ReportSummary(
            TotalSales: totalSales,
            TransactionCount: transactionCount,
            AverageValue: averageValue,
            TotalDiscounts: totalDiscounts,
            TotalGrossMargin: totalGrossMargin);
    }

    private static List<ReportCategorySummary> AggregateByCategoryName(IReadOnlyList<ReportLineItemRow> rows)
    {
        return rows
            .GroupBy(r => r.CategoryName)
            .Select(g =>
            {
                var totalSales = Math.Round(g.Sum(r => r.LineAmount), 2, MidpointRounding.AwayFromZero);
                var txCount = g.Select(r => r.TransactionNumber).Distinct().Count();
                var totalGrossMargin = Math.Round(g.Sum(r => r.GrossMargin), 2, MidpointRounding.AwayFromZero);
                var avgMargin = g.Any()
                    ? Math.Round(g.Average(r => r.RealizedMarginPercentage), 2, MidpointRounding.AwayFromZero)
                    : 0m;

                return new ReportCategorySummary(
                    CategoryName: g.Key,
                    TotalSales: totalSales,
                    TransactionCount: txCount,
                    TotalGrossMargin: totalGrossMargin,
                    AverageRealizedMarginPercentage: avgMargin);
            })
            .OrderByDescending(c => c.TotalSales)
            .ToList();
    }

    private static List<ReportProductSummary> AggregateByProduct(IReadOnlyList<ReportLineItemRow> rows)
    {
        return rows
            .GroupBy(r => new { r.ProductName, r.CategoryName })
            .Select(g =>
            {
                var totalQty = g.Sum(r => r.Quantity);
                var totalSales = Math.Round(g.Sum(r => r.LineAmount), 2, MidpointRounding.AwayFromZero);
                var totalGrossMargin = Math.Round(g.Sum(r => r.GrossMargin), 2, MidpointRounding.AwayFromZero);
                var avgMargin = g.Any()
                    ? Math.Round(g.Average(r => r.RealizedMarginPercentage), 2, MidpointRounding.AwayFromZero)
                    : 0m;

                return new ReportProductSummary(
                    ProductName: g.Key.ProductName,
                    CategoryName: g.Key.CategoryName,
                    TotalQuantitySold: totalQty,
                    TotalSales: totalSales,
                    TotalGrossMargin: totalGrossMargin,
                    AverageRealizedMarginPercentage: avgMargin);
            })
            .OrderByDescending(p => p.TotalSales)
            .ToList();
    }
}
