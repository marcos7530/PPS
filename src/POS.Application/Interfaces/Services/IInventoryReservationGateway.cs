using POS.Application.DTOs;
using POS.Domain.Common;

namespace POS.Application.Interfaces.Services;

/// <summary>
/// Gateway port for inventory stock locking and adjustment (Req 9.21, 9.22, 11.13, 20.7).
/// Implementations must lock product rows in deterministic order (product_id ASC)
/// with UPDLOCK, ROWLOCK, HOLDLOCK to prevent deadlocks.
/// </summary>
public interface IInventoryReservationGateway
{
    /// <summary>
    /// Locks product rows and adjusts stock quantities atomically.
    /// Returns updated stock levels keyed by product ID.
    /// </summary>
    Task<Result<IReadOnlyDictionary<Guid, int>>> LockAndAdjustAsync(
        IReadOnlyList<StockDelta> deltas, CancellationToken ct);
}
