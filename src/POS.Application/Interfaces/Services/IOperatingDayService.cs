namespace POS.Application.Interfaces.Services;

/// <summary>
/// Derives the operating day from a UTC instant and the configured business timezone.
/// The operating day is immutable once persisted (Req 9.19).
/// </summary>
public interface IOperatingDayService
{
    /// <summary>
    /// Converts a UTC instant to the local date in the given business timezone.
    /// </summary>
    DateOnly DeriveOperatingDay(DateTimeOffset utcInstant, string businessTimeZone);

    /// <summary>
    /// Gets the current operating day using IClock.UtcNow and the given business timezone.
    /// </summary>
    DateOnly GetCurrentOperatingDay(string businessTimeZone);
}
