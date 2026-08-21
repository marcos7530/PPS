using System.Globalization;

namespace POS.Domain.ValueObjects;

/// <summary>
/// Represents a percentage value between 0.00 and 1000.00 with exactly 2 decimal places.
/// </summary>
public readonly record struct Percentage
{
    public decimal Value { get; }

    private Percentage(decimal value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a Percentage value. Must be between 0.00 and 1000.00.
    /// </summary>
    public static Result<Percentage> Create(decimal value)
    {
        var rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);

        if (rounded < 0.00m || rounded > 1000.00m)
            return Result<Percentage>.Failure(ErrorCode.InvalidDiscountPercentage);

        return Result<Percentage>.Success(new Percentage(rounded));
    }

    /// <summary>
    /// Applies this percentage to a Money amount, returning the computed portion.
    /// </summary>
    public Money ApplyTo(Money baseAmount) =>
        new(baseAmount.Amount * Value / 100m);

    public override string ToString() => $"{Value.ToString("F2", CultureInfo.InvariantCulture)}%";
}
