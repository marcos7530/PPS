namespace POS.Application.Views;

/// <summary>
/// A single result from a customer search operation (Req 13.5).
/// </summary>
public sealed record CustomerSearchResult(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    bool IsActive);
