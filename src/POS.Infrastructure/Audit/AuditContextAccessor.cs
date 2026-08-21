using POS.Application.Interfaces.Audit;

namespace POS.Infrastructure.Audit;

/// <summary>
/// Scoped implementation of <see cref="IAuditContextAccessor"/>.
/// Set once per request by the authentication middleware.
/// </summary>
public sealed class AuditContextAccessor : IAuditContextAccessor
{
    public Guid? UserId { get; private set; }
    public string UsernameSnapshot { get; private set; } = string.Empty;
    public Guid? SessionId { get; private set; }
    public string? IpAddress { get; private set; }

    public void Apply(Guid? userId, string usernameSnapshot, Guid? sessionId, string? ipAddress)
    {
        UserId = userId;
        UsernameSnapshot = usernameSnapshot;
        SessionId = sessionId;
        IpAddress = ipAddress;
    }
}
