using POS.Application.DTOs;
using POS.Domain.Common;

namespace POS.Application.Interfaces.Services;

/// <summary>
/// Service port for receipt emission and reprinting (Req 17).
/// </summary>
public interface IReceiptService
{
    /// <summary>
    /// Emits a receipt for a completed transaction or return.
    /// </summary>
    Task<Result<ReceiptDocument>> EmitAsync(ReceiptSource src, ReceiptChannel channel, CancellationToken ct);

    /// <summary>
    /// Reprints a previously emitted receipt.
    /// </summary>
    Task<Result<ReceiptDocument>> ReprintAsync(Guid txOrReturnId, ReceiptChannel channel, CancellationToken ct);
}
