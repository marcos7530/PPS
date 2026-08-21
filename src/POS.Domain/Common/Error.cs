namespace POS.Domain.Common;

/// <summary>
/// Represents a domain error with a code and optional contextual arguments.
/// </summary>
public readonly record struct DomainError(ErrorCode Code, IReadOnlyDictionary<string, object?> Args)
{
    public static DomainError Create(ErrorCode code) =>
        new(code, new Dictionary<string, object?>());

    public static DomainError Create(ErrorCode code, string key, object? value) =>
        new(code, new Dictionary<string, object?> { { key, value } });

    public static DomainError Create(ErrorCode code, IReadOnlyDictionary<string, object?> args) =>
        new(code, args);
}
