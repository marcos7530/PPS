namespace POS.Application.DTOs;

/// <summary>
/// Request to elevate permissions via manager authorization without changing the active session.
/// </summary>
public sealed record ElevationRequest(
    string ManagerUsername,
    string ManagerPassword,
    string RequiredPermission,
    Guid RequestingUserId);
