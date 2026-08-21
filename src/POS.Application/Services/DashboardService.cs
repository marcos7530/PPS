using System.Text.Json;
using POS.Application.DTOs;
using POS.Application.Interfaces.Repositories;
using POS.Application.Interfaces.Services;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Application.Services;

/// <summary>
/// Manages dashboard configuration and provides aggregated metrics (Req 8.1-8.10, 20.15).
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private const int MaxWidgets = 8;
    private const int MaxDateRangeDays = 366;
    private const int DefaultRangeDays = 30;
    private const int TopProductsCount = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly IReadOnlyList<WidgetConfig> DefaultWidgets = new List<WidgetConfig>
    {
        new(WidgetConfig.Types.DailySalesLine, 0),
        new(WidgetConfig.Types.TopProductsBar, 1),
        new(WidgetConfig.Types.SalesByCategoryPie, 2),
        new(WidgetConfig.Types.TotalSalesNumeric, 3)
    };

    private readonly IDashboardConfigurationRepository _configRepository;
    private readonly IDailySalesAggregateRepository _aggregateRepository;
    private readonly IClock _clock;

    public DashboardService(
        IDashboardConfigurationRepository configRepository,
        IDailySalesAggregateRepository aggregateRepository,
        IClock clock)
    {
        _configRepository = configRepository;
        _aggregateRepository = aggregateRepository;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<WidgetConfig>>> GetConfigurationAsync(Guid userId, CancellationToken ct = default)
    {
        var config = await _configRepository.GetByUserIdAsync(userId, ct);

        if (config is null || string.IsNullOrWhiteSpace(config.Widgets))
            return Result<IReadOnlyList<WidgetConfig>>.Success(DefaultWidgets);

        var widgets = DeserializeWidgets(config.Widgets);
        return Result<IReadOnlyList<WidgetConfig>>.Success(widgets);
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<WidgetConfig>>> SaveConfigurationAsync(
        Guid userId, IReadOnlyList<WidgetConfig> widgets, CancellationToken ct = default)
    {
        // Validate max 8 widgets (Req 8.2)
        if (widgets.Count > MaxWidgets)
            return Result<IReadOnlyList<WidgetConfig>>.Failure(ErrorCode.DateRangeExceedsLimit);

        // Validate widget types
        foreach (var widget in widgets)
        {
            if (!WidgetConfig.Types.All.Contains(widget.Type))
                return Result<IReadOnlyList<WidgetConfig>>.Failure(ErrorCode.UnexpectedError);
        }

        var json = JsonSerializer.Serialize(widgets, JsonOptions);

        var existing = await _configRepository.GetByUserIdAsync(userId, ct);

        if (existing is not null)
        {
            existing.Widgets = json;
            existing.UpdatedAt = _clock.UtcNow;
            _configRepository.Update(existing);
        }
        else
        {
            var newConfig = new DashboardConfiguration
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Widgets = json,
                UpdatedAt = _clock.UtcNow
            };
            await _configRepository.AddAsync(newConfig, ct);
        }

        return Result<IReadOnlyList<WidgetConfig>>.Success(widgets);
    }

    /// <inheritdoc/>
    public async Task<Result<DashboardData>> GetMetricsAsync(
        Guid userId, DateOnly? dateFrom, DateOnly? dateTo, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);

        var from = dateFrom ?? today.AddDays(-DefaultRangeDays + 1);
        var to = dateTo ?? today;

        // Validate date range (Req 8.8: max 366 days)
        var daySpan = to.DayNumber - from.DayNumber + 1;
        if (daySpan < 1 || daySpan > MaxDateRangeDays)
            return Result<DashboardData>.Failure(ErrorCode.DateRangeExceedsLimit);

        try
        {
            // Load pre-aggregated data (already excludes voided transactions — Req 20.15)
            var aggregates = await _aggregateRepository.GetByDateRangeAsync(from, to, ct);

            if (aggregates.Count == 0)
            {
                // Req 8.10: Empty data
                var emptyData = new DashboardData(
                    SalesByDay: [],
                    TopProducts: [],
                    SalesByCategory: [],
                    TotalSales: 0m,
                    TransactionCount: 0,
                    HasData: false,
                    ErrorMessage: "No data available for selected date range");

                return Result<DashboardData>.Success(emptyData);
            }

            // Sales by day (Req 8.1)
            var salesByDay = aggregates
                .GroupBy(a => a.OperatingDay)
                .Select(g => new DayMetric(
                    Day: g.Key,
                    Amount: Math.Round(g.Sum(a => a.NetSalesAmount), 2, MidpointRounding.AwayFromZero),
                    TxCount: g.Sum(a => a.TransactionCount)))
                .OrderBy(d => d.Day)
                .ToList();

            // Top 10 products by quantity (Req 8.1)
            var topProducts = aggregates
                .GroupBy(a => new { a.ProductId, ProductName = a.Product?.Name ?? "Unknown" })
                .Select(g => new ProductMetric(
                    ProductName: g.Key.ProductName,
                    QuantitySold: g.Sum(a => a.QuantitySold),
                    TotalSales: Math.Round(g.Sum(a => a.NetSalesAmount), 2, MidpointRounding.AwayFromZero)))
                .OrderByDescending(p => p.QuantitySold)
                .Take(TopProductsCount)
                .ToList();

            // Sales by category (Req 8.1)
            var totalSales = Math.Round(aggregates.Sum(a => a.NetSalesAmount), 2, MidpointRounding.AwayFromZero);
            var salesByCategory = aggregates
                .GroupBy(a => new { a.CategoryId, CategoryName = a.Category?.Name ?? "Unknown" })
                .Select(g =>
                {
                    var catSales = Math.Round(g.Sum(a => a.NetSalesAmount), 2, MidpointRounding.AwayFromZero);
                    var percentage = totalSales > 0
                        ? Math.Round(catSales / totalSales * 100m, 2, MidpointRounding.AwayFromZero)
                        : 0m;
                    return new CategoryMetric(
                        CategoryName: g.Key.CategoryName,
                        TotalSales: catSales,
                        Percentage: percentage);
                })
                .OrderByDescending(c => c.TotalSales)
                .ToList();

            // Transaction count
            var transactionCount = aggregates.Sum(a => a.TransactionCount);

            var data = new DashboardData(
                SalesByDay: salesByDay,
                TopProducts: topProducts,
                SalesByCategory: salesByCategory,
                TotalSales: totalSales,
                TransactionCount: transactionCount,
                HasData: true,
                ErrorMessage: null);

            return Result<DashboardData>.Success(data);
        }
        catch (Exception)
        {
            // Req 8.5: On error, display error message on affected widgets
            var errorData = new DashboardData(
                SalesByDay: [],
                TopProducts: [],
                SalesByCategory: [],
                TotalSales: 0m,
                TransactionCount: 0,
                HasData: false,
                ErrorMessage: "Unable to load dashboard data");

            return Result<DashboardData>.Success(errorData);
        }
    }

    private static IReadOnlyList<WidgetConfig> DeserializeWidgets(string json)
    {
        try
        {
            var widgets = JsonSerializer.Deserialize<List<WidgetConfig>>(json, JsonOptions);
            return widgets ?? (IReadOnlyList<WidgetConfig>)DefaultWidgets;
        }
        catch (JsonException)
        {
            return DefaultWidgets;
        }
    }
}
