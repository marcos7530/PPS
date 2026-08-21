using POS.Application.DTOs;
using POS.Domain.Common;

namespace POS.Application.Interfaces.Audit;

/// <summary>
/// Port for enqueueing audit entries atomically with operations.
/// Enqueue drafts are materialized in the same SaveChanges transaction.
/// If audit write fails, the entire operation is rolled back (Req 1.1, 1.8).
/// </summary>
public interface IAuditWriter
{
    /// <summary>
    /// Enqueues an audit entry draft to be materialized during SaveChanges.
    /// </summary>
    void Enqueue(AuditEntryDraft draft);

    /// <summary>
    /// Writes a standalone audit entry for a failed operation attempt (Req 1.2).
    /// </summary>
    Task WriteFailedAttemptAsync(ErrorCode code, AuditContext ctx, CancellationToken ct);
}
