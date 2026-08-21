using System.Globalization;

namespace POS.Domain.ValueObjects;

/// <summary>
/// Represents a valid cash denomination for cash count breakdown.
/// </summary>
public readonly record struct Denomination : IComparable<Denomination>
{
    private static readonly HashSet<decimal> ValidDenominations = new()
    {
        100.00m, 50.00m, 20.00m, 10.00m, 5.00m, 1.00m, 0.25m, 0.10m, 0.05m, 0.01m
    };

    public decimal Value { get; }

    private Denomination(decimal value)
    {
        Value = value;
    }

    /// <summary>
    /// All valid denominations ordered from largest to smallest.
    /// </summary>
    public static IReadOnlyList<Denomination> All { get; } = new[]
    {
        new Denomination(100.00m),
        new Denomination(50.00m),
        new Denomination(20.00m),
        new Denomination(10.00m),
        new Denomination(5.00m),
        new Denomination(1.00m),
        new Denomination(0.25m),
        new Denomination(0.10m),
        new Denomination(0.05m),
        new Denomination(0.01m)
    };

    /// <summary>
    /// Creates a Denomination if the value is a valid cash denomination.
    /// </summary>
    public static Result<Denomination> Create(decimal value)
    {
        if (!ValidDenominations.Contains(value))
            return Result<Denomination>.Failure(DomainError.Create(
                ErrorCode.UnexpectedError, "denomination", value));

        return Result<Denomination>.Success(new Denomination(value));
    }

    /// <summary>
    /// Computes the total money for a given count of this denomination.
    /// </summary>
    public Money Total(int count) => new(Value * count);

    public int CompareTo(Denomination other) => other.Value.CompareTo(Value); // Descending

    public static bool operator >(Denomination left, Denomination right) =>
        left.Value > right.Value;

    public static bool operator <(Denomination left, Denomination right) =>
        left.Value < right.Value;

    public static bool operator >=(Denomination left, Denomination right) =>
        left.Value >= right.Value;

    public static bool operator <=(Denomination left, Denomination right) =>
        left.Value <= right.Value;

    public override string ToString() => Value.ToString("F2", CultureInfo.InvariantCulture);
}
