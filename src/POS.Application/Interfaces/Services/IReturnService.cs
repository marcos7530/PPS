using POS.Application.Commands;
using POS.Application.Views;
using POS.Domain.Common;

namespace POS.Application.Interfaces.Services;

/// <summary>
/// Service port for return/refund operations (Req 11).
/// </summary>
public interface IReturnService
{
    /// <summary>
    /// Loads a transaction view showing returnable line items and quantities.
    /// </summary>
    Task<Result<ReturnableTransactionView>> LoadReturnableAsync(Guid originalTxId, CancellationToken ct);

    /// <summary>
    /// Completes a return: adjusts inventory, issues refund, emits receipt.
    /// </summary>
    Task<Result<CompletedReturn>> CompleteAsync(CompleteReturnCommand cmd, CancellationToken ct);
}
