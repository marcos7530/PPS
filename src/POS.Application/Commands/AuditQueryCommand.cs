namespace POS.Application.Commands;

/// <summary>
/// Command to query the audit log with optional filters (Req 1.4, 1.5).
/// </summary>
public sealed record AuditQueryCommand(
    DateTimeOffset DateFrom,
    DateTimeOffset DateTo,
    Guid? UserId,
    string? OperationType,
    Guid PerformedBy);
