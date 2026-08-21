namespace POS.Application.Views;

/// <summary>
/// Full customer detail view including profile and purchase history (Req 13.9).
/// </summary>
public sealed record CustomerView(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    Guid CreatedBy);
