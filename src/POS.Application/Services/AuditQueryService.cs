using POS.Application.Commands;
using POS.Application.Interfaces.Repositories;
using POS.Application.Interfaces.Services;
using POS.Application.Views;
using POS.Domain.Common;

namespace POS.Application.Services;

/// <summary>
/// Implements audit log querying with date range validation,
/// filtering, and 10,000-entry pagination (Req 1.4, 1.5).
/// </summary>
public sealed class AuditQueryService : IAuditQueryService
{
    private const int MaxResults = 10_000;
    private const int MaxDateRangeDays = 366;

    private readonly IAuditLogRepository _auditLogRepository;

    public AuditQueryService(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    /// <summary>
    /// Queries the audit log with optional filters (Req 1.4, 1.5).
    /// Validates date range does not exceed 366 days.
    /// Returns up to 10,000 most recent entries with total count indication.
    /// </summary>
    public async Task<Result<AuditQueryResult>> QueryAsync(AuditQueryCommand cmd, CancellationToken ct)
    {
        // Validate date range does not exceed 366 days
        var dateRangeDays = (cmd.DateTo - cmd.DateFrom).TotalDays;
        if (dateRangeDays > MaxDateRangeDays || dateRangeDays < 0)
        {
            return Result<AuditQueryResult>.Failure(ErrorCode.DateRangeExceedsLimit);
        }

        // Query the repository with filters, limited to MaxResults
        var (entries, totalCount) = await _auditLogRepository.QueryAsync(
            cmd.DateFrom,
            cmd.DateTo,
            cmd.UserId,
            cmd.OperationType,
            MaxResults,
            ct);

        // Req 1.5: Indicate if more entries exist beyond the 10,000 limit
        var hasMore = totalCount > MaxResults;

        return Result<AuditQueryResult>.Success(
            new AuditQueryResult(entries, totalCount, hasMore));
    }
}
