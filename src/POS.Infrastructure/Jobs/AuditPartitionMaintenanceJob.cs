using Microsoft.Extensions.Logging;
using POS.Domain.Abstractions;
using POS.Infrastructure.Data;
using Quartz;

namespace POS.Infrastructure.Jobs;

/// <summary>
/// Quartz job that creates next month's partition for the audit_log table.
/// Runs monthly to ensure partitions exist ahead of time.
/// </summary>
[DisallowConcurrentExecution]
public sealed partial class AuditPartitionMaintenanceJob : IJob
{
    private readonly PosDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<AuditPartitionMaintenanceJob> _logger;

    public AuditPartitionMaintenanceJob(PosDbContext db, IClock clock, ILogger<AuditPartitionMaintenanceJob> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var now = _clock.UtcNow;
        // Create partition for the month after next (ensures we always have next month ready)
        var targetMonth = now.AddMonths(2);
        var boundaryValue = new DateTimeOffset(targetMonth.Year, targetMonth.Month, 1, 0, 0, 0, TimeSpan.Zero);

        // Execute raw SQL to split the partition for audit_log monthly range.
        // The partition function/scheme is created in the initial migration.
        var boundaryString = boundaryValue.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz", System.Globalization.CultureInfo.InvariantCulture);
        var sql = $"""
            ALTER PARTITION SCHEME ps_audit_monthly NEXT USED [PRIMARY];
            ALTER PARTITION FUNCTION pf_audit_monthly() SPLIT RANGE ('{boundaryString}');
            """;

        try
        {
            await _db.Database.ExecuteSqlRawAsync(sql, context.CancellationToken);
            LogPartitionCreated(_logger, boundaryValue);
        }
        catch (Exception ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                                   || ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
        {
            // Partition boundary already exists — idempotent, no action needed
            LogPartitionExists(_logger, boundaryValue);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created audit_log partition boundary for {BoundaryDate}")]
    private static partial void LogPartitionCreated(ILogger logger, DateTimeOffset boundaryDate);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Partition boundary for {BoundaryDate} already exists, skipping")]
    private static partial void LogPartitionExists(ILogger logger, DateTimeOffset boundaryDate);
}
