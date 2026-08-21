namespace POS.Application.DTOs;

/// <summary>
/// Returned on successful login. Contains the raw session token (to set in cookie)
/// and session metadata.
/// </summary>
public sealed record LoginResult(
    Guid SessionId,
    Guid UserId,
    string Username,
    string RawToken,
    DateTimeOffset ExpiresAt);
