using Microsoft.Extensions.Logging;
using POS.Domain.Abstractions;
using POS.Domain.Entities;
using POS.Infrastructure.Data;
using Quartz;

namespace POS.Infrastructure.Jobs;

/// <summary>
/// Quartz job that recalculates DailySalesAggregate from transaction data for recent days.
/// Runs every 15 minutes to keep dashboard data current.
/// </summary>
[DisallowConcurrentExecution]
public sealed partial class RefreshDashboardAggregatesJob : IJob
{
    private readonly PosDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<RefreshDashboardAggregatesJob> _logger;

    /// <summary>
    /// Number of recent days to recalculate aggregates for.
    /// </summary>
    private const int RecentDaysWindow = 3;

    public RefreshDashboardAggregatesJob(PosDbContext db, IClock clock, ILogger<RefreshDashboardAggregatesJob> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var now = _clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var fromDate = today.AddDays(-RecentDaysWindow);
        var ct = context.CancellationToken;

        // Aggregate transaction line items grouped by operating day, category, and product
        var aggregates = await _db.TransactionLineItems
            .Where(li => li.Transaction.OperatingDay >= fromDate
                         && !li.Transaction.IsVoided)
            .GroupBy(li => new
            {
                li.Transaction.OperatingDay,
                li.Product.CategoryId,
                li.ProductId
            })
            .Select(g => new
            {
                g.Key.OperatingDay,
                g.Key.CategoryId,
                g.Key.ProductId,
                NetSalesAmount = g.Sum(li => li.LineAmount),
                TransactionCount = g.Select(li => li.TransactionId).Distinct().Count(),
                QuantitySold = g.Sum(li => li.Quantity),
                GrossMarginAmount = g.Sum(li => li.LineAmount - (li.RecordedCostPrice ?? 0m) * li.Quantity)
            })
            .ToListAsync(ct);

        if (aggregates.Count == 0)
        {
            LogNoData(_logger);
            return;
        }

        // Remove existing aggregates for the date range and reinsert
        var existingAggregates = await _db.DailySalesAggregates
            .Where(a => a.OperatingDay >= fromDate)
            .ToListAsync(ct);

        _db.DailySalesAggregates.RemoveRange(existingAggregates);

        foreach (var agg in aggregates)
        {
            _db.DailySalesAggregates.Add(new DailySalesAggregate
            {
                OperatingDay = agg.OperatingDay,
                CategoryId = agg.CategoryId,
                ProductId = agg.ProductId,
                NetSalesAmount = agg.NetSalesAmount,
                TransactionCount = agg.TransactionCount,
                QuantitySold = agg.QuantitySold,
                GrossMarginAmount = agg.GrossMarginAmount,
                RefreshedAt = now
            });
        }

        await _db.SaveChangesAsync(ct);

        LogRefreshed(_logger, aggregates.Count, fromDate, today);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "No transaction data found for aggregation window")]
    private static partial void LogNoData(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Refreshed {Count} dashboard aggregate(s) for operating days {From} to {To}")]
    private static partial void LogRefreshed(ILogger logger, int count, DateOnly from, DateOnly to);
}
