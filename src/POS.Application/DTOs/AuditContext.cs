namespace POS.Application.DTOs;

/// <summary>
/// Context for auditing a failed operation attempt.
/// </summary>
public sealed record AuditContext(
    Guid? UserId,
    string UsernameSnapshot,
    Guid? SessionId,
    string? IpAddress,
    string EntityType,
    Guid? EntityId,
    string? Metadata);
