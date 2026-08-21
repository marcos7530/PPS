using POS.Application.Commands;
using POS.Application.Views;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Application.Interfaces.Services;

/// <summary>
/// Service port for cash register shift operations (Req 12).
/// </summary>
public interface IShiftService
{
    /// <summary>
    /// Opens a new shift on the specified cash drawer.
    /// </summary>
    Task<Result<Shift>> OpenAsync(OpenShiftCommand cmd, CancellationToken ct);

    /// <summary>
    /// Calculates the expected cash balance for the shift (Req 12.8).
    /// </summary>
    Task<Result<Money>> GetExpectedCashAsync(Guid shiftId, CancellationToken ct);

    /// <summary>
    /// Closes the shift with denomination count and variance calculation.
    /// </summary>
    Task<Result<ShiftSummary>> CloseAsync(CloseShiftCommand cmd, CancellationToken ct);

    /// <summary>
    /// Records a cash withdrawal or deposit during a shift.
    /// </summary>
    Task<Result<CashMovement>> RecordMovementAsync(CashMovementCommand cmd, CancellationToken ct);
}
