using POS.Application.Commands;
using POS.Application.Views;
using POS.Domain.Common;

namespace POS.Application.Interfaces.Services;

/// <summary>
/// Service port for querying audit log entries (Req 1.4, 1.5).
/// </summary>
public interface IAuditQueryService
{
    /// <summary>
    /// Queries the audit log with optional date range, user, and operation type filters.
    /// Returns up to 10,000 entries ordered by most recent first.
    /// </summary>
    Task<Result<AuditQueryResult>> QueryAsync(AuditQueryCommand cmd, CancellationToken ct);
}
