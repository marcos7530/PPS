using POS.Application.Common;
using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Repositories;
using POS.Application.Interfaces.Services;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Application.Services;

/// <summary>
/// Manages store credit consumption (voucher and customer balance) and restoration on void (Req 9.8-9.15, 20.9).
/// </summary>
public sealed class StoreCreditService : IStoreCreditService
{
    private readonly IStoreCreditVoucherRepository _voucherRepository;
    private readonly IStoreCreditRepository _storeCreditRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IClock _clock;
    private readonly IAuditWriter _auditWriter;

    public StoreCreditService(
        IStoreCreditVoucherRepository voucherRepository,
        IStoreCreditRepository storeCreditRepository,
        ITransactionRepository transactionRepository,
        IClock clock,
        IAuditWriter auditWriter)
    {
        _voucherRepository = voucherRepository;
        _storeCreditRepository = storeCreditRepository;
        _transactionRepository = transactionRepository;
        _clock = clock;
        _auditWriter = auditWriter;
    }

    /// <summary>
    /// Consumes store credit (voucher or customer balance) up to <paramref name="maxAmount"/>.
    /// If VoucherCode is provided, uses voucher; otherwise uses customer balance (Req 9.8-9.15).
    /// </summary>
    public async Task<Result<AppliedStoreCredit>> ConsumeAsync(
        StoreCreditRequest req, Money maxAmount, CancellationToken ct)
    {
        if (maxAmount.Amount <= 0)
            return Result<AppliedStoreCredit>.Failure(ErrorCode.AdditionalPaymentRequired);

        if (!string.IsNullOrWhiteSpace(req.VoucherCode))
        {
            return await ConsumeVoucherAsync(req, maxAmount, ct);
        }

        return await ConsumeBalanceAsync(req, maxAmount, ct);
    }

    /// <summary>
    /// Restores store credit consumed in a voided transaction (Req 20.9).
    /// Resets vouchers to unused and restores customer balance.
    /// </summary>
    public async Task<Result<Unit>> RestoreAsync(Guid transactionId, CancellationToken ct)
    {
        var transaction = await _transactionRepository.GetWithPaymentsAsync(transactionId, ct);
        if (transaction is null)
            return Result<Unit>.Failure(ErrorCode.InvalidOrExpiredTransaction);

        var storeCreditPayments = transaction.Payments
            .Where(p => p.Method == "store_credit" && p.IsConsumptionActive)
            .ToList();

        if (storeCreditPayments.Count == 0)
            return Result<Unit>.Success(Unit.Value);

        var now = _clock.UtcNow;

        foreach (var payment in storeCreditPayments)
        {
            if (payment.VoucherId.HasValue)
            {
                // Restore voucher to unused
                var voucher = await _voucherRepository.GetByIdAsync(payment.VoucherId.Value, ct);
                if (voucher is not null)
                {
                    var beforeState = $"{{\"status\":\"{voucher.Status}\",\"usedAt\":\"{voucher.UsedAt}\"}}";
                    voucher.Status = "unused";
                    voucher.UsedAt = null;
                    voucher.UsedInTransactionId = null;
                    _voucherRepository.Update(voucher);

                    _auditWriter.Enqueue(new AuditEntryDraft(
                        OperationType: "RestoreStoreCredit_Voucher",
                        EntityType: "StoreCreditVoucher",
                        EntityId: voucher.Id,
                        RelatedEntityIds: new List<Guid> { transactionId },
                        BeforeState: beforeState,
                        AfterState: $"{{\"status\":\"unused\",\"usedAt\":null}}",
                        Metadata: $"{{\"transactionId\":\"{transactionId}\",\"amount\":{payment.Amount}}}"));
                }
            }
            else if (payment.StoreCreditCustomerId.HasValue)
            {
                // Restore customer balance
                var storeCredit = await _storeCreditRepository.GetByCustomerIdAsync(
                    payment.StoreCreditCustomerId.Value, ct);
                if (storeCredit is not null)
                {
                    var previousBalance = storeCredit.Balance;
                    storeCredit.Balance += payment.Amount;
                    storeCredit.UpdatedAt = now;
                    _storeCreditRepository.Update(storeCredit);

                    _auditWriter.Enqueue(new AuditEntryDraft(
                        OperationType: "RestoreStoreCredit_Balance",
                        EntityType: "StoreCredit",
                        EntityId: storeCredit.Id,
                        RelatedEntityIds: new List<Guid> { transactionId },
                        BeforeState: $"{{\"balance\":{previousBalance}}}",
                        AfterState: $"{{\"balance\":{storeCredit.Balance}}}",
                        Metadata: $"{{\"transactionId\":\"{transactionId}\",\"restoredAmount\":{payment.Amount}}}"));
                }
            }

            // Mark payment as no longer active
            payment.IsConsumptionActive = false;
        }

        return Result<Unit>.Success(Unit.Value);
    }

    private async Task<Result<AppliedStoreCredit>> ConsumeVoucherAsync(
        StoreCreditRequest req, Money maxAmount, CancellationToken ct)
    {
        var voucher = await _voucherRepository.GetByCodeAsync(req.VoucherCode!, ct);

        if (voucher is null)
            return Result<AppliedStoreCredit>.Failure(ErrorCode.VoucherNotFound);

        if (voucher.IsUsed)
            return Result<AppliedStoreCredit>.Failure(ErrorCode.VoucherAlreadyUsed);

        var now = _clock.UtcNow;
        if (voucher.IsExpired(now))
            return Result<AppliedStoreCredit>.Failure(ErrorCode.VoucherExpired);

        // Apply min(voucher.amount, maxAmount) with 2-decimal precision
        var applicableAmount = new Money(Math.Min(voucher.Amount, maxAmount.Amount));

        // Mark voucher as used
        var beforeState = $"{{\"status\":\"{voucher.Status}\",\"amount\":{voucher.Amount}}}";
        voucher.Status = "used";
        voucher.UsedAt = now;
        voucher.UsedInTransactionId = req.TransactionId;
        _voucherRepository.Update(voucher);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "ConsumeStoreCredit_Voucher",
            EntityType: "StoreCreditVoucher",
            EntityId: voucher.Id,
            RelatedEntityIds: new List<Guid> { req.TransactionId },
            BeforeState: beforeState,
            AfterState: $"{{\"status\":\"used\",\"usedAt\":\"{now:O}\",\"usedInTransactionId\":\"{req.TransactionId}\"}}",
            Metadata: $"{{\"amountApplied\":{applicableAmount.Amount},\"voucherTotal\":{voucher.Amount}}}"));

        var remainingBalance = new Money(voucher.Amount - applicableAmount.Amount);
        return Result<AppliedStoreCredit>.Success(
            new AppliedStoreCredit(applicableAmount, remainingBalance));
    }

    private async Task<Result<AppliedStoreCredit>> ConsumeBalanceAsync(
        StoreCreditRequest req, Money maxAmount, CancellationToken ct)
    {
        var storeCredit = await _storeCreditRepository.GetByCustomerIdAsync(req.CustomerId, ct);

        if (storeCredit is null || storeCredit.Balance <= 0)
            return Result<AppliedStoreCredit>.Failure(ErrorCode.CustomerHasNoStoreCredit);

        // Apply min(balance, maxAmount) with 2-decimal precision
        var applicableAmount = new Money(Math.Min(storeCredit.Balance, maxAmount.Amount));

        var previousBalance = storeCredit.Balance;
        storeCredit.Balance -= applicableAmount.Amount;
        storeCredit.UpdatedAt = _clock.UtcNow;
        _storeCreditRepository.Update(storeCredit);

        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "ConsumeStoreCredit_Balance",
            EntityType: "StoreCredit",
            EntityId: storeCredit.Id,
            RelatedEntityIds: new List<Guid> { req.TransactionId },
            BeforeState: $"{{\"balance\":{previousBalance}}}",
            AfterState: $"{{\"balance\":{storeCredit.Balance}}}",
            Metadata: $"{{\"amountApplied\":{applicableAmount.Amount},\"customerId\":\"{req.CustomerId}\"}}"));

        var remainingBalance = new Money(storeCredit.Balance);
        return Result<AppliedStoreCredit>.Success(
            new AppliedStoreCredit(applicableAmount, remainingBalance));
    }
}
