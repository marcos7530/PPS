using POS.Application.Commands;
using POS.Application.Views;
using POS.Domain.Common;

namespace POS.Application.Interfaces.Services;

/// <summary>
/// Service port for transaction void operations (Req 20).
/// </summary>
public interface IVoidService
{
    /// <summary>
    /// Voids a transaction: restores inventory, reverses store credit, marks as voided.
    /// </summary>
    Task<Result<VoidedTransactionView>> VoidAsync(VoidCommand cmd, CancellationToken ct);
}
