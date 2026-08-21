using POS.Application.Commands;
using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Repositories;
using POS.Application.Interfaces.Services;
using POS.Application.Views;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Application.Services;

/// <summary>
/// Manages return/refund operations: loading returnable transactions,
/// validating quantities, issuing refunds, and adjusting inventory (Req 11.1-11.16).
/// </summary>
public sealed class ReturnService : IReturnService
{
    private const int ReturnWindowDays = 90;
    private const decimal ManagerAuthorizationThreshold = 500.00m;

    private static readonly HashSet<string> ValidRefundMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "cash", "credit_card_reversal", "store_credit"
    };

    private static readonly HashSet<string> ValidReasonCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "defective_product", "customer_regret", "wrong_product", "other"
    };

    private readonly ITransactionRepository _transactionRepository;
    private readonly IReturnRepository _returnRepository;
    private readonly IShiftRepository _shiftRepository;
    private readonly IStoreCreditRepository _storeCreditRepository;
    private readonly IStoreCreditVoucherRepository _storeCreditVoucherRepository;
    private readonly ISystemConfigurationRepository _configRepository;
    private readonly IInventoryReservationGateway _inventoryGateway;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _auditWriter;

    public ReturnService(
        ITransactionRepository transactionRepository,
        IReturnRepository returnRepository,
        IShiftRepository shiftRepository,
        IStoreCreditRepository storeCreditRepository,
        IStoreCreditVoucherRepository storeCreditVoucherRepository,
        ISystemConfigurationRepository configRepository,
        IInventoryReservationGateway inventoryGateway,
        IClock clock,
        IUnitOfWork unitOfWork,
        IAuditWriter auditWriter)
    {
        _transactionRepository = transactionRepository;
        _returnRepository = returnRepository;
        _shiftRepository = shiftRepository;
        _storeCreditRepository = storeCreditRepository;
        _storeCreditVoucherRepository = storeCreditVoucherRepository;
        _configRepository = configRepository;
        _inventoryGateway = inventoryGateway;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _auditWriter = auditWriter;
    }

    /// <summary>
    /// Loads a transaction view showing returnable line items and quantities (Req 11.1-11.5).
    /// Validates that the transaction exists, is not voided, and is within the 90-day return window.
    /// </summary>
    public async Task<Result<ReturnableTransactionView>> LoadReturnableAsync(
        Guid originalTxId, CancellationToken ct)
    {
        var transaction = await _transactionRepository.GetWithLineItemsAsync(originalTxId, ct);

        if (transaction is null)
            return Result<ReturnableTransactionView>.Failure(ErrorCode.InvalidOrExpiredTransaction);

        // Req 11.2: Reject if voided
        if (transaction.IsVoided)
            return Result<ReturnableTransactionView>.Failure(ErrorCode.TransactionVoidedCannotBeReturned);

        // Req 11.2: Reject if older than 90 days
        var now = _clock.UtcNow;
        if ((now - transaction.CompletedAt).TotalDays > ReturnWindowDays)
            return Result<ReturnableTransactionView>.Failure(ErrorCode.InvalidOrExpiredTransaction);

        // Build returnable line items view (Req 11.3-11.4)
        var lines = transaction.LineItems.Select(li =>
        {
            var returnableQty = li.Quantity - li.ReturnedQuantity;
            return new ReturnableLineItemView(
                LineItemId: li.Id,
                ProductId: li.ProductId,
                ProductName: li.ProductNameSnapshot,
                OriginalQuantity: li.Quantity,
                AlreadyReturnedQuantity: li.ReturnedQuantity,
                ReturnableQuantity: returnableQty,
                UnitPrice: new Money(li.UnitPrice));
        }).ToList();

        return Result<ReturnableTransactionView>.Success(new ReturnableTransactionView(
            TransactionId: transaction.Id,
            TransactionNumber: transaction.TransactionNumber,
            CompletedAt: transaction.CompletedAt,
            Lines: lines));
    }

    /// <summary>
    /// Completes a return: validates inputs, adjusts inventory, issues refund,
    /// creates audit entry, and persists atomically (Req 11.5-11.16).
    /// </summary>
    public async Task<Result<CompletedReturn>> CompleteAsync(
        CompleteReturnCommand cmd, CancellationToken ct)
    {
        // Validate refund method and reason code (Req 11.7)
        if (!ValidRefundMethods.Contains(cmd.RefundMethod))
            return Result<CompletedReturn>.Failure(ErrorCode.InvalidOrExpiredTransaction);

        if (!ValidReasonCodes.Contains(cmd.ReasonCode))
            return Result<CompletedReturn>.Failure(ErrorCode.InvalidOrExpiredTransaction);

        if (cmd.Lines is null || cmd.Lines.Count == 0)
            return Result<CompletedReturn>.Failure(ErrorCode.InvalidOrExpiredTransaction);

        // Load and validate original transaction
        var transaction = await _transactionRepository.GetWithLineItemsAsync(cmd.OriginalTransactionId, ct);

        if (transaction is null)
            return Result<CompletedReturn>.Failure(ErrorCode.InvalidOrExpiredTransaction);

        if (transaction.IsVoided)
            return Result<CompletedReturn>.Failure(ErrorCode.TransactionVoidedCannotBeReturned);

        var now = _clock.UtcNow;
        if ((now - transaction.CompletedAt).TotalDays > ReturnWindowDays)
            return Result<CompletedReturn>.Failure(ErrorCode.InvalidOrExpiredTransaction);

        // Validate return quantities against original line items (Req 11.4-11.5)
        var returnLineItems = new List<ReturnLineItem>();
        decimal totalRefund = 0m;

        foreach (var line in cmd.Lines)
        {
            var originalLine = transaction.LineItems.FirstOrDefault(li => li.Id == line.OriginalLineItemId);
            if (originalLine is null)
                return Result<CompletedReturn>.Failure(ErrorCode.InvalidOrExpiredTransaction);

            var availableQty = originalLine.Quantity - originalLine.ReturnedQuantity;
            if (line.Quantity < 1 || line.Quantity > availableQty)
                return Result<CompletedReturn>.Failure(ErrorCode.ReturnQuantityExceedsOriginal);

            // Req 11.6: refund = return_qty × unit_price with 2 decimal precision
            var lineRefund = Math.Round(line.Quantity * originalLine.UnitPrice, 2, MidpointRounding.AwayFromZero);
            totalRefund += lineRefund;

            returnLineItems.Add(new ReturnLineItem
            {
                Id = Guid.NewGuid(),
                LineItemId = originalLine.Id,
                ProductId = originalLine.ProductId,
                ReturnQuantity = line.Quantity,
                UnitPrice = originalLine.UnitPrice,
                LineRefundAmount = lineRefund
            });
        }

        var refundAmount = new Money(totalRefund);

        // Req 11.8: Cash refund requires active shift
        Shift? activeShift = null;
        if (cmd.RefundMethod.Equals("cash", StringComparison.OrdinalIgnoreCase))
        {
            activeShift = await _shiftRepository.GetActiveByUserIdAsync(cmd.UserId, ct);
            if (activeShift is null)
                return Result<CompletedReturn>.Failure(ErrorCode.NoActiveShiftForCashRefund);
        }

        // Req 11.10-11.11: Manager authorization required for store_credit or amount > 500
        var requiresAuthorization =
            cmd.RefundMethod.Equals("store_credit", StringComparison.OrdinalIgnoreCase) ||
            refundAmount.Amount > ManagerAuthorizationThreshold;

        if (requiresAuthorization && !cmd.AuthorizedBy.HasValue)
            return Result<CompletedReturn>.Failure(ErrorCode.ManagerAuthorizationRequiredForRefund);

        // Load system configuration for timezone
        var config = await _configRepository.GetAsync(ct);
        var operatingDay = DeriveOperatingDay(now, config.BusinessTimeZone);

        // Req 11.12: Generate unique return identifier (UUID v4)
        var returnId = Guid.NewGuid();

        // Build Return entity
        var returnEntity = new Return
        {
            Id = returnId,
            OriginalTransactionId = cmd.OriginalTransactionId,
            CompletedAt = now,
            OperatingDay = operatingDay,
            UserId = cmd.UserId,
            ShiftId = activeShift?.Id,
            RefundAmount = refundAmount.Amount,
            RefundMethod = cmd.RefundMethod.ToLowerInvariant(),
            ReasonCode = cmd.ReasonCode.ToLowerInvariant(),
            AuthorizedBy = cmd.AuthorizedBy,
            LineItems = returnLineItems
        };

        // Set ReturnId on all line items
        foreach (var rli in returnLineItems)
        {
            rli.ReturnId = returnId;
        }

        // Begin atomic transaction
        string? voucherCode = null;

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // Req 11.13: Atomically increment inventory for returned products
            var stockDeltas = returnLineItems
                .Select(rli => new StockDelta(rli.ProductId, rli.ReturnQuantity))
                .ToList();

            var inventoryResult = await _inventoryGateway.LockAndAdjustAsync(stockDeltas, ct);
            if (!inventoryResult.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<CompletedReturn>.Failure(ErrorCode.ReturnInventoryUpdateFailed);
            }

            // Update ReturnedQuantity on original line items
            foreach (var line in cmd.Lines)
            {
                var originalLine = transaction.LineItems.First(li => li.Id == line.OriginalLineItemId);
                originalLine.ReturnedQuantity += line.Quantity;
            }
            _transactionRepository.Update(transaction);

            // Req 11.15: Handle store credit refund
            if (cmd.RefundMethod.Equals("store_credit", StringComparison.OrdinalIgnoreCase))
            {
                voucherCode = await IssueStoreCreditAsync(
                    transaction, returnId, refundAmount, now, ct);
            }

            // Persist the return entity
            await _returnRepository.AddAsync(returnEntity, ct);

            // Req 11.16: Record return in AuditLog
            var lineItemsJson = string.Join(",", returnLineItems.Select(rli =>
                $"{{\"lineItemId\":\"{rli.LineItemId}\",\"productId\":\"{rli.ProductId}\",\"returnQty\":{rli.ReturnQuantity},\"unitPrice\":{rli.UnitPrice},\"lineRefund\":{rli.LineRefundAmount}}}"));

            var metadataParts = new List<string>
            {
                $"\"originalTransactionId\":\"{cmd.OriginalTransactionId}\"",
                $"\"refundMethod\":\"{returnEntity.RefundMethod}\"",
                $"\"reasonCode\":\"{returnEntity.ReasonCode}\""
            };
            if (activeShift is not null)
                metadataParts.Add($"\"shiftId\":\"{activeShift.Id}\"");
            if (cmd.AuthorizedBy.HasValue)
                metadataParts.Add($"\"authorizedBy\":\"{cmd.AuthorizedBy.Value}\"");
            if (voucherCode is not null)
                metadataParts.Add($"\"voucherCode\":\"{voucherCode}\"");
            var metadataJson = "{" + string.Join(",", metadataParts) + "}";

            _auditWriter.Enqueue(new AuditEntryDraft(
                OperationType: "CompleteReturn",
                EntityType: "Return",
                EntityId: returnId,
                RelatedEntityIds: returnLineItems.Select(rli => rli.ProductId).Distinct().ToList(),
                BeforeState: null,
                AfterState: $"{{\"returnId\":\"{returnId}\",\"userId\":\"{cmd.UserId}\",\"refundAmount\":{refundAmount.Amount},\"lineItems\":[{lineItemsJson}]}}",
                Metadata: metadataJson));

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        return Result<CompletedReturn>.Success(new CompletedReturn(
            ReturnId: returnId,
            OriginalTransactionId: cmd.OriginalTransactionId,
            RefundAmount: refundAmount,
            RefundMethod: returnEntity.RefundMethod,
            VoucherCode: voucherCode,
            CompletedAt: now));
    }

    #region Private Helpers

    /// <summary>
    /// Issues store credit for a return: if the original transaction had a customer,
    /// credits their balance; otherwise generates a 32-char voucher (Req 11.15).
    /// </summary>
    private async Task<string?> IssueStoreCreditAsync(
        Transaction transaction,
        Guid returnId,
        Money refundAmount,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (transaction.CustomerId.HasValue)
        {
            // Credit existing customer balance
            var storeCredit = await _storeCreditRepository.GetByCustomerIdAsync(
                transaction.CustomerId.Value, ct);

            if (storeCredit is not null)
            {
                var previousBalance = storeCredit.Balance;
                storeCredit.Balance += refundAmount.Amount;
                storeCredit.UpdatedAt = now;
                _storeCreditRepository.Update(storeCredit);

                _auditWriter.Enqueue(new AuditEntryDraft(
                    OperationType: "IssueStoreCredit_Balance",
                    EntityType: "StoreCredit",
                    EntityId: storeCredit.Id,
                    RelatedEntityIds: new List<Guid> { returnId },
                    BeforeState: $"{{\"balance\":{previousBalance}}}",
                    AfterState: $"{{\"balance\":{storeCredit.Balance}}}",
                    Metadata: $"{{\"returnId\":\"{returnId}\",\"amount\":{refundAmount.Amount}}}"));
            }
            else
            {
                // Create new store credit record for customer
                var newStoreCredit = new StoreCredit
                {
                    Id = Guid.NewGuid(),
                    CustomerId = transaction.CustomerId.Value,
                    Balance = refundAmount.Amount,
                    UpdatedAt = now
                };
                await _storeCreditRepository.AddAsync(newStoreCredit, ct);

                _auditWriter.Enqueue(new AuditEntryDraft(
                    OperationType: "IssueStoreCredit_NewBalance",
                    EntityType: "StoreCredit",
                    EntityId: newStoreCredit.Id,
                    RelatedEntityIds: new List<Guid> { returnId },
                    BeforeState: null,
                    AfterState: $"{{\"balance\":{newStoreCredit.Balance},\"customerId\":\"{transaction.CustomerId.Value}\"}}",
                    Metadata: $"{{\"returnId\":\"{returnId}\",\"amount\":{refundAmount.Amount}}}"));
            }

            return null; // No voucher needed for known customers
        }
        else
        {
            // Generate 32-character alphanumeric voucher code (Req 11.15)
            var code = GenerateVoucherCode();

            var voucher = new StoreCreditVoucher
            {
                Id = Guid.NewGuid(),
                Code = code,
                Amount = refundAmount.Amount,
                IssuedAt = now,
                ExpiresAt = now.AddDays(365),
                Status = "unused",
                OriginReturnId = returnId
            };
            await _storeCreditVoucherRepository.AddAsync(voucher, ct);

            _auditWriter.Enqueue(new AuditEntryDraft(
                OperationType: "IssueStoreCredit_Voucher",
                EntityType: "StoreCreditVoucher",
                EntityId: voucher.Id,
                RelatedEntityIds: new List<Guid> { returnId },
                BeforeState: null,
                AfterState: $"{{\"code\":\"{code}\",\"amount\":{voucher.Amount},\"expiresAt\":\"{voucher.ExpiresAt:O}\"}}",
                Metadata: $"{{\"returnId\":\"{returnId}\",\"amount\":{refundAmount.Amount}}}"));

            return code;
        }
    }

    /// <summary>
    /// Generates a 32-character alphanumeric voucher code using cryptographically random bytes.
    /// </summary>
    private static string GenerateVoucherCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var code = new char[32];
        for (int i = 0; i < 32; i++)
        {
            code[i] = chars[bytes[i] % chars.Length];
        }
        return new string(code);
    }

    private static DateOnly DeriveOperatingDay(DateTimeOffset utcNow, string businessTimeZone)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(businessTimeZone);
            var localTime = TimeZoneInfo.ConvertTime(utcNow, tz);
            return DateOnly.FromDateTime(localTime.DateTime);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(utcNow.UtcDateTime);
        }
    }

    #endregion
}
