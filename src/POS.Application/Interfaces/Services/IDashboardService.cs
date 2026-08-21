using POS.Application.DTOs;
using POS.Domain.Common;

namespace POS.Application.Interfaces.Services;

/// <summary>
/// Service port for dashboard configuration and metric retrieval (Req 8.1-8.10, 20.15).
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Retrieves the widget configuration for a user. Returns default config if none exists.
    /// </summary>
    Task<Result<IReadOnlyList<WidgetConfig>>> GetConfigurationAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Saves widget configuration for a user. Validates max 8 widgets and valid types.
    /// </summary>
    Task<Result<IReadOnlyList<WidgetConfig>>> SaveConfigurationAsync(Guid userId, IReadOnlyList<WidgetConfig> widgets, CancellationToken ct = default);

    /// <summary>
    /// Retrieves dashboard metrics for the specified date range.
    /// Uses DailySalesAggregate data (excludes voided transactions).
    /// </summary>
    Task<Result<DashboardData>> GetMetricsAsync(Guid userId, DateOnly? dateFrom, DateOnly? dateTo, CancellationToken ct = default);
}
