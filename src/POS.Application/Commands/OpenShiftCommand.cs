namespace POS.Application.Commands;

/// <summary>
/// Command to open a new cash register shift.
/// </summary>
public sealed record OpenShiftCommand(
    string CashDrawerId,
    Guid UserId,
    decimal OpeningCashAmount,
    IReadOnlyList<DenominationCount> Denominations);
