using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetByUserAsync(Guid userId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetByDateRangeAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default);

    /// <summary>
    /// Queries audit log entries with filtering by date range, user, and operation type.
    /// Returns up to maxResults entries ordered by OccurredAt descending (most recent first)
    /// along with the total count of matching entries (Req 1.4, 1.5).
    /// The date range filter enables partition elimination on the audit_log table.
    /// </summary>
    Task<(IReadOnlyList<AuditLog> entries, int totalCount)> QueryAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        Guid? userId,
        string? operationType,
        int maxResults,
        CancellationToken ct = default);
}
