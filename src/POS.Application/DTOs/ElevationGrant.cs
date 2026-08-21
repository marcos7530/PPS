namespace POS.Application.DTOs;

/// <summary>
/// Grant issued after a successful manager elevation authorization.
/// </summary>
public sealed record ElevationGrant(
    Guid AuthorizingUserId,
    string AuthorizingUsername,
    string PermissionGranted,
    DateTimeOffset GrantedAt);
