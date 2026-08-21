namespace POS.Application.DTOs;

/// <summary>
/// Draft of an audit entry to be materialized during SaveChanges.
/// </summary>
public sealed record AuditEntryDraft(
    string OperationType,
    string EntityType,
    Guid? EntityId,
    IReadOnlyList<Guid>? RelatedEntityIds,
    string? BeforeState,
    string? AfterState,
    string? Metadata);
