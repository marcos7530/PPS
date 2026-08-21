using POS.Application.Interfaces.Services;
using POS.Domain.Abstractions;

namespace POS.Application.Services;

/// <summary>
/// Derives the business operating day from a UTC instant and the configured timezone.
/// Uses TimeZoneInfo.FindSystemTimeZoneById which supports IANA identifiers on .NET 8.
/// </summary>
public sealed class OperatingDayService : IOperatingDayService
{
    private readonly IClock _clock;

    public OperatingDayService(IClock clock)
    {
        _clock = clock;
    }

    /// <inheritdoc />
    public DateOnly DeriveOperatingDay(DateTimeOffset utcInstant, string businessTimeZone)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(businessTimeZone);
        var localTime = TimeZoneInfo.ConvertTime(utcInstant, tz);
        return DateOnly.FromDateTime(localTime.DateTime);
    }

    /// <inheritdoc />
    public DateOnly GetCurrentOperatingDay(string businessTimeZone)
    {
        return DeriveOperatingDay(_clock.UtcNow, businessTimeZone);
    }
}
