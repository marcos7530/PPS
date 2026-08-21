using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetByUserAsync(Guid userId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLog>> GetByDateRangeAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default);
}
