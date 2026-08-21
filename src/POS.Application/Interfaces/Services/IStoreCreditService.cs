using POS.Application.Common;
using POS.Application.DTOs;
using POS.Domain.Common;
using POS.Domain.ValueObjects;

namespace POS.Application.Interfaces.Services;

/// <summary>
/// Service port for store credit consumption and restoration (Req 9.8-9.15, 11.15, 20.9).
/// </summary>
public interface IStoreCreditService
{
    /// <summary>
    /// Consumes store credit (balance or voucher) up to the specified maximum amount.
    /// </summary>
    Task<Result<AppliedStoreCredit>> ConsumeAsync(StoreCreditRequest req, Money maxAmount, CancellationToken ct);

    /// <summary>
    /// Restores store credit consumed in a voided transaction.
    /// </summary>
    Task<Result<Unit>> RestoreAsync(Guid transactionId, CancellationToken ct);
}
