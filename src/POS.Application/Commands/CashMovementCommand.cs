namespace POS.Application.Commands;

/// <summary>
/// Command to record a cash withdrawal or deposit during a shift.
/// </summary>
public sealed record CashMovementCommand(
    Guid ShiftId,
    Guid UserId,
    string MovementType,
    decimal Amount,
    string Reason,
    string? Notes);
