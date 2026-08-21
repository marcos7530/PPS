using POS.Application.Commands;
using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Repositories;
using POS.Application.Interfaces.Services;
using POS.Application.Views;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Application.Services;

/// <summary>
/// Implements transaction void operations with atomic inventory restoration,
/// store credit reversal, shift balance adjustment, and audit logging (Req 20.1-20.19).
/// </summary>
public sealed class VoidService : IVoidService
{
    private static readonly HashSet<string> ValidVoidReasons = new(StringComparer.OrdinalIgnoreCase)
    {
        "cashier_error", "customer_cancellation", "pricing_error", "duplicate_transaction", "other"
    };

    private readonly ITransactionRepository _transactionRepository;
    private readonly IReturnRepository _returnRepository;
    private readonly IShiftRepository _shiftRepository;
    private readonly IUserRepository _userRepository;
    private readonly ISystemConfigurationRepository _configRepository;
    private readonly IInventoryReservationGateway _inventoryGateway;
    private readonly IStoreCreditService _storeCreditService;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _auditWriter;

    public VoidService(
        ITransactionRepository transactionRepository,
        IReturnRepository returnRepository,
        IShiftRepository shiftRepository,
        IUserRepository userRepository,
        ISystemConfigurationRepository configRepository,
        IInventoryReservationGateway inventoryGateway,
        IStoreCreditService storeCreditService,
        IClock clock,
        IUnitOfWork unitOfWork,
        IAuditWriter auditWriter)
    {
        _transactionRepository = transactionRepository;
        _returnRepository = returnRepository;
        _shiftRepository = shiftRepository;
        _userRepository = userRepository;
        _configRepository = configRepository;
        _inventoryGateway = inventoryGateway;
        _storeCreditService = storeCreditService;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _auditWriter = auditWriter;
    }

    /// <summary>
    /// Voids a completed transaction: validates preconditions, atomically restores inventory,
    /// reverses store credit, adjusts shift cash balance, and records audit entry (Req 20.1-20.19).
    /// </summary>
    public async Task<Result<VoidedTransactionView>> VoidAsync(VoidCommand cmd, CancellationToken ct)
    {
        // Req 20.5, 20.6: Validate void reason and notes
        if (string.IsNullOrWhiteSpace(cmd.VoidReason) ||
            !ValidVoidReasons.Contains(cmd.VoidReason) ||
            string.IsNullOrWhiteSpace(cmd.VoidNotes) ||
            cmd.VoidNotes.Length < 1 ||
            cmd.VoidNotes.Length > 500)
        {
            return Result<VoidedTransactionView>.Failure(ErrorCode.VoidReasonAndNotesRequired);
        }

        // Req 20.1, 20.2: Validate user has Manager or Administrator role
        var user = await _userRepository.GetByIdWithRolesAsync(cmd.VoidedBy, ct);
        if (user is null)
            return Result<VoidedTransactionView>.Failure(ErrorCode.InsufficientPermissions);

        var hasPrivilege = user.UserRoles.Any(ur =>
            ur.RoleId == Role.WellKnown.AdministratorId ||
            ur.RoleId == Role.WellKnown.ManagerId);

        if (!hasPrivilege)
        {
            // Check if AuthorizedBy provides manager/admin elevation
            if (cmd.AuthorizedBy.HasValue)
            {
                var authorizer = await _userRepository.GetByIdWithRolesAsync(cmd.AuthorizedBy.Value, ct);
                if (authorizer is null || !authorizer.UserRoles.Any(ur =>
                    ur.RoleId == Role.WellKnown.AdministratorId ||
                    ur.RoleId == Role.WellKnown.ManagerId))
                {
                    return Result<VoidedTransactionView>.Failure(ErrorCode.ManagerAuthorizationRequiredToVoid);
                }
            }
            else
            {
                return Result<VoidedTransactionView>.Failure(ErrorCode.ManagerAuthorizationRequiredToVoid);
            }
        }

        // Load transaction with all related data
        var transaction = await _transactionRepository.GetFullAsync(cmd.TransactionId, ct);
        if (transaction is null)
            return Result<VoidedTransactionView>.Failure(ErrorCode.InvalidOrExpiredTransaction);

        // Req 20.11: Check if already voided
        if (transaction.IsVoided)
            return Result<VoidedTransactionView>.Failure(ErrorCode.TransactionAlreadyVoided);

        // Req 20.13: Check if transaction has existing returns
        var returns = await _returnRepository.GetByOriginalTransactionIdAsync(cmd.TransactionId, ct);
        if (returns.Count > 0)
            return Result<VoidedTransactionView>.Failure(ErrorCode.TransactionHasReturns);

        // Req 20.1, 20.3: Validate same operating day
        var config = await _configRepository.GetAsync(ct);
        var now = _clock.UtcNow;
        var currentOperatingDay = DeriveOperatingDay(now, config.BusinessTimeZone);

        if (transaction.OperatingDay != currentOperatingDay)
            return Result<VoidedTransactionView>.Failure(ErrorCode.TransactionBelongsToClosedOperatingDay);

        // Req 20.1, 20.4: Validate shift is still open
        if (transaction.ShiftId.HasValue)
        {
            var shift = await _shiftRepository.GetByIdAsync(transaction.ShiftId.Value, ct);
            if (shift is null || shift.Status != "open")
                return Result<VoidedTransactionView>.Failure(ErrorCode.ShiftAlreadyClosed);
        }

        // Begin atomic transaction
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // Req 20.7, 20.18: Atomically restore inventory for all line items
            var stockDeltas = transaction.LineItems
                .Select(li => new StockDelta(li.ProductId, li.Quantity)) // positive = restock
                .ToList();

            var inventoryResult = await _inventoryGateway.LockAndAdjustAsync(stockDeltas, ct);
            if (!inventoryResult.IsSuccess)
            {
                await _unitOfWork.RollbackAsync(ct);
                return Result<VoidedTransactionView>.Failure(ErrorCode.VoidInventoryRestoreFailed);
            }

            // Req 20.8: Subtract cash amount from shift expected balance
            if (transaction.ShiftId.HasValue)
            {
                var cashPayments = transaction.Payments
                    .Where(p => p.Method == "cash")
                    .ToList();

                if (cashPayments.Count > 0)
                {
                    var cashAmount = cashPayments.Sum(p => p.Amount);
                    var shift = await _shiftRepository.GetByIdAsync(transaction.ShiftId.Value, ct);
                    if (shift is not null)
                    {
                        shift.ExpectedCashBalance = (shift.ExpectedCashBalance ?? 0m) - cashAmount;
                        _shiftRepository.Update(shift);
                    }
                }
            }

            // Req 20.9: Restore store credit (voucher to unused or customer balance)
            var hasStoreCredit = transaction.Payments.Any(p =>
                p.Method == "store_credit" && p.IsConsumptionActive);

            if (hasStoreCredit)
            {
                var restoreResult = await _storeCreditService.RestoreAsync(cmd.TransactionId, ct);
                if (!restoreResult.IsSuccess)
                {
                    await _unitOfWork.RollbackAsync(ct);
                    return Result<VoidedTransactionView>.Failure(restoreResult.Error!.Value);
                }
            }

            // Req 20.10: Mark transaction as voided (preserve record, line items, receipts)
            transaction.IsVoided = true;
            transaction.VoidedAt = now;
            transaction.VoidedBy = cmd.VoidedBy;
            transaction.VoidReason = cmd.VoidReason.ToLowerInvariant();
            transaction.VoidNotes = cmd.VoidNotes;
            _transactionRepository.Update(transaction);

            // Req 20.17: Record void details in AuditLog
            var paymentMethods = string.Join(",",
                transaction.Payments.Select(p => $"\"{p.Method}\"").Distinct());

            _auditWriter.Enqueue(new AuditEntryDraft(
                OperationType: "VoidTransaction",
                EntityType: "Transaction",
                EntityId: transaction.Id,
                RelatedEntityIds: transaction.LineItems
                    .Select(li => li.ProductId).Distinct().ToList(),
                BeforeState: $"{{\"isVoided\":false,\"finalAmount\":{transaction.FinalAmount}}}",
                AfterState: $"{{\"isVoided\":true,\"voidedAt\":\"{now:O}\",\"voidedBy\":\"{cmd.VoidedBy}\",\"voidReason\":\"{transaction.VoidReason}\",\"voidNotes\":\"{EscapeJson(cmd.VoidNotes)}\"}}",
                Metadata: $"{{\"transactionNumber\":{transaction.TransactionNumber},\"voidedAmount\":{transaction.FinalAmount:F2},\"paymentMethods\":[{paymentMethods}],\"shiftId\":\"{transaction.ShiftId}\",\"voidReason\":\"{transaction.VoidReason}\",\"notes\":\"{EscapeJson(cmd.VoidNotes)}\"}}"));

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        return Result<VoidedTransactionView>.Success(new VoidedTransactionView(
            transaction.Id,
            transaction.TransactionNumber,
            transaction.VoidReason!,
            now));
    }

    #region Private Helpers

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

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    #endregion
}
