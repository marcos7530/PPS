using System.Globalization;

namespace POS.Domain.ValueObjects;

/// <summary>
/// Represents an operating day derived from a UTC instant and a timezone (IANA format).
/// </summary>
public readonly record struct OperatingDay
{
    public DateOnly Date { get; }
    public string TimeZoneId { get; }

    private OperatingDay(DateOnly date, string timeZoneId)
    {
        Date = date;
        TimeZoneId = timeZoneId;
    }

    /// <summary>
    /// Creates an OperatingDay by converting a UTC instant to the specified timezone.
    /// </summary>
    /// <param name="utcInstant">The UTC date/time instant.</param>
    /// <param name="timeZoneId">IANA timezone identifier (e.g., "America/Guatemala").</param>
    public static OperatingDay FromUtcInstant(DateTimeOffset utcInstant, string timeZoneId)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var localTime = TimeZoneInfo.ConvertTime(utcInstant, tz);
        return new OperatingDay(DateOnly.FromDateTime(localTime.DateTime), timeZoneId);
    }

    /// <summary>
    /// Creates an OperatingDay from an explicit date (for cases where the date is already known).
    /// </summary>
    public static OperatingDay FromDate(DateOnly date, string timeZoneId)
    {
        // Validate that the timezone exists
        _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return new OperatingDay(date, timeZoneId);
    }

    public override string ToString() => Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
