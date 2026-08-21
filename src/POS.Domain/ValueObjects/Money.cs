using System.Globalization;

namespace POS.Domain.ValueObjects;

/// <summary>
/// Represents a monetary amount with exactly 2 decimal places and half-up rounding.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    public decimal Amount { get; }

    public Money(decimal amount)
    {
        Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    public static Money Zero => new(0m);

    public static Money operator +(Money left, Money right) =>
        new(left.Amount + right.Amount);

    public static Money operator -(Money left, Money right) =>
        new(left.Amount - right.Amount);

    public static Money operator *(Money money, decimal multiplier) =>
        new(money.Amount * multiplier);

    public static Money operator *(decimal multiplier, Money money) =>
        new(money.Amount * multiplier);

    public static Money operator -(Money money) =>
        new(-money.Amount);

    public static bool operator >(Money left, Money right) =>
        left.Amount > right.Amount;

    public static bool operator <(Money left, Money right) =>
        left.Amount < right.Amount;

    public static bool operator >=(Money left, Money right) =>
        left.Amount >= right.Amount;

    public static bool operator <=(Money left, Money right) =>
        left.Amount <= right.Amount;

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

    public override string ToString() => Amount.ToString("F2", CultureInfo.InvariantCulture);
}
