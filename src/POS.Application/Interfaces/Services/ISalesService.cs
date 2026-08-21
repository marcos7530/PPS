using POS.Application.Commands;
using POS.Application.Views;
using POS.Domain.Common;

namespace POS.Application.Interfaces.Services;

/// <summary>
/// Service port for sales transaction operations (Req 9, 18.11-18.16, 19).
/// </summary>
public interface ISalesService
{
    /// <summary>
    /// Adds a line item to an open transaction by product ID.
    /// </summary>
    Task<Result<OpenTransactionView>> AddLineItemAsync(Guid txId, Guid productId, int qty, CancellationToken ct);

    /// <summary>
    /// Adds a line item to an open transaction by barcode scan.
    /// </summary>
    Task<Result<OpenTransactionView>> AddByBarcodeAsync(Guid txId, string barcode, CancellationToken ct);

    /// <summary>
    /// Applies a percentage discount to a line item.
    /// </summary>
    Task<Result<OpenTransactionView>> ApplyLineDiscountAsync(ApplyDiscountCommand cmd, CancellationToken ct);

    /// <summary>
    /// Completes the transaction: validates payment, adjusts inventory, emits receipt.
    /// </summary>
    Task<Result<CompletedSale>> CompleteAsync(CompleteSaleCommand cmd, CancellationToken ct);
}
