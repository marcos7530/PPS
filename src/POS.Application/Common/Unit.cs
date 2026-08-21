namespace POS.Application.Common;

/// <summary>
/// Represents a void return type for Result&lt;T&gt; when no value is needed.
/// </summary>
public readonly record struct Unit
{
    public static Unit Value => default;
}
