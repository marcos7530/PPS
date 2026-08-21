namespace POS.Application.DTOs;

/// <summary>
/// Input for the login operation.
/// </summary>
public sealed record LoginRequest(
    string Username,
    string Password,
    string? IpAddress,
    string? UserAgent);
