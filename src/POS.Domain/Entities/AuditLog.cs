namespace POS.Domain.Entities;

/// <summary>
/// Immutable, append-only audit log entry. No updates or deletes allowed.
/// Constructed once and never modified.
/// </summary>
public class AuditLog
{
    [Key]
    public Guid Id { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public Guid? UserId { get; init; }

    [Required, MaxLength(50)]
    public string UsernameSnapshot { get; init; } = string.Empty;

    [Required, MaxLength(40)]
    public string OperationType { get; init; } = string.Empty;

    [Required, MaxLength(40)]
    public string EntityType { get; init; } = string.Empty;

    public Guid? EntityId { get; init; }

    /// <summary>
    /// JSON array of related entity IDs.
    /// </summary>
    public string? RelatedEntityIds { get; init; }

    /// <summary>
    /// Allowed: success, failure.
    /// </summary>
    [Required, MaxLength(10)]
    public string Outcome { get; init; } = string.Empty;

    [MaxLength(60)]
    public string? ErrorCode { get; init; }

    /// <summary>
    /// JSON snapshot of entity state before the operation.
    /// </summary>
    public string? BeforeState { get; init; }

    /// <summary>
    /// JSON snapshot of entity state after the operation.
    /// </summary>
    public string? AfterState { get; init; }

    /// <summary>
    /// Additional JSON metadata.
    /// </summary>
    public string? Metadata { get; init; }

    public Guid? SessionId { get; init; }

    [MaxLength(45)]
    public string? IpAddress { get; init; }

    // Navigation properties (read-only references)
    [ForeignKey(nameof(UserId))]
    public User? User { get; init; }
}
