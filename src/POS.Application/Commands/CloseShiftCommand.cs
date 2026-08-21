namespace POS.Application.Commands;

/// <summary>
/// Command to close an active shift with denomination breakdown.
/// </summary>
public sealed record CloseShiftCommand(
    Guid ShiftId,
    Guid UserId,
    decimal ClosingCashAmount,
    IReadOnlyList<DenominationCount> Denominations,
    string? VarianceNotes);

/// <summary>
/// Count of a specific denomination during cash counting.
/// </summary>
public sealed record DenominationCount(
    decimal DenominationValue,
    int Quantity);
