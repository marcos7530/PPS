using POS.Domain.ValueObjects;

namespace POS.Application.Views;

/// <summary>
/// Summary of a closed shift with variance information.
/// </summary>
public sealed record ShiftSummary(
    Guid ShiftId,
    Money OpeningCash,
    Money ExpectedCash,
    Money ClosingCash,
    Money Variance,
    string VarianceStatus,
    int TransactionCount,
    int ReturnCount,
    DateTimeOffset OpenedAt,
    DateTimeOffset ClosedAt);
