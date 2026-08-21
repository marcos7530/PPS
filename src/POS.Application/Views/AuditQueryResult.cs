using POS.Domain.Entities;

namespace POS.Application.Views;

/// <summary>
/// Result of an audit log query containing entries and pagination metadata (Req 1.4, 1.5).
/// </summary>
public sealed record AuditQueryResult(
    IReadOnlyList<AuditLog> Entries,
    int TotalCount,
    bool HasMore);
