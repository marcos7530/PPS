namespace POS.Application.Interfaces.Audit;

/// <summary>
/// Provides access to the current user context for audit trail entries.
/// Scoped per request — set by the authentication layer at request start.
/// </summary>
public interface IAuditContextAccessor
{
    Guid? UserId { get; }
    string UsernameSnapshot { get; }
    Guid? SessionId { get; }
    string? IpAddress { get; }

    void Apply(Guid? userId, string usernameSnapshot, Guid? sessionId, string? ipAddress);
}
