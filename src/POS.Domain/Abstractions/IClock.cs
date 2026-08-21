namespace POS.Domain.Abstractions;

/// <summary>
/// Abstraction for the system clock. Provides a single time source for the domain.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
