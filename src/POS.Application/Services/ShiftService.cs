using System.Text.Json;
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
/// Implements cash register shift operations: open, close, cash movements,
/// expected cash calculation, and variance tracking (Req 12.1-12.15).
/// </summary>
public sealed class ShiftService : IShiftService
{
    private static readonly HashSet<string> ValidMovementTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "withdrawal", "deposit"
    };

    private readonly IShiftRepository _shiftRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IReturnRepository _returnRepository;
    private readonly ICashMovementRepository _cashMovementRepository;
    private readonly ICashCountRepository _cashCountRepository;
    private readonly IUserRepository _userRepository;
    private readonly ISystemConfigurationRepository _configRepository;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _auditWriter;

    public ShiftService(
        IShiftRepository shiftRepository,
        ITransactionRepository transactionRepository,
        IReturnRepository returnRepository,
        ICashMovementRepository cashMovementRepository,
        ICashCountRepository cashCountRepository,
        IUserRepository userRepository,
        ISystemConfigurationRepository configRepository,
        IClock clock,
        IUnitOfWork unitOfWork,
        IAuditWriter auditWriter)
    {
        _shiftRepository = shiftRepository;
        _transactionRepository = transactionRepository;
        _returnRepository = returnRepository;
        _cashMovementRepository = cashMovementRepository;
        _cashCountRepository = cashCountRepository;
        _userRepository = userRepository;
        _configRepository = configRepository;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _auditWriter = auditWriter;
    }

    /// <summary>
    /// Opens a new shift on the specified cash drawer (Req 12.1-12.4).
    /// Validates one shift per user and one per drawer, records denomination breakdown.
    /// </summary>
    public async Task<Result<Shift>> OpenAsync(OpenShiftCommand cmd, CancellationToken ct)
    {
        // Validate CashDrawerId (1-20 chars)
        if (string.IsNullOrWhiteSpace(cmd.CashDrawerId) || cmd.CashDrawerId.Length > 20)
            return Result<Shift>.Failure(ErrorCode.UnexpectedError);

        // Validate opening amount range (0.00-999999.99)
        if (cmd.OpeningCashAmount < 0m || cmd.OpeningCashAmount > 999999.99m)
            return Result<Shift>.Failure(ErrorCode.UnexpectedError);

        // Validate denominations
        if (cmd.Denominations is null || cmd.Denominations.Count == 0)
            return Result<Shift>.Failure(ErrorCode.UnexpectedError);

        foreach (var denom in cmd.Denominations)
        {
            var denomResult = Denomination.Create(denom.DenominationValue);
            if (!denomResult.IsSuccess)
                return Result<Shift>.Failure(ErrorCode.UnexpectedError);

            if (denom.Quantity < 0)
                return Result<Shift>.Failure(ErrorCode.UnexpectedError);
        }

        // Req 12.2: Reject if cash drawer already has an active shift
        var existingDrawerShift = await _shiftRepository.GetActiveByDrawerIdAsync(cmd.CashDrawerId, ct);
        if (existingDrawerShift is not null)
            return Result<Shift>.Failure(ErrorCode.CashDrawerHasActiveShift);

        // Req 12.3: Reject if user already has an active shift
        var existingUserShift = await _shiftRepository.GetActiveByUserIdAsync(cmd.UserId, ct);
        if (existingUserShift is not null)
            return Result<Shift>.Failure(ErrorCode.UserHasActiveShift);

        var now = _clock.UtcNow;
        var config = await _configRepository.GetAsync(ct);
        var operatingDay = DeriveOperatingDay(now, config.BusinessTimeZone);

        // Req 12.4: Generate UUID v4 shift, record opening timestamp, user, drawer, amount
        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            CashDrawerId = cmd.CashDrawerId,
            UserId = cmd.UserId,
            OpenedAt = now,
            OperatingDay = operatingDay,
            OpeningCashAmount = cmd.OpeningCashAmount,
            Status = "open"
        };

        // Create opening cash count with denomination breakdown
        var breakdownJson = SerializeDenominations(cmd.Denominations);
        var cashCount = new CashCount
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            CountType = "opening",
            TotalAmount = cmd.OpeningCashAmount,
            Breakdown = breakdownJson,
            CountedAt = now,
            CountedBy = cmd.UserId
        };

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _shiftRepository.AddAsync(shift, ct);
            await _cashCountRepository.AddRangeAsync(new[] { cashCount }, ct);

            // Req 12.7: Log in AuditLog
            _auditWriter.Enqueue(new AuditEntryDraft(
                OperationType: "OpenShift",
                EntityType: "Shift",
                EntityId: shift.Id,
                RelatedEntityIds: null,
                BeforeState: null,
                AfterState: JsonSerializer.Serialize(new
                {
                    shiftId = shift.Id,
                    cashDrawerId = shift.CashDrawerId,
                    userId = shift.UserId,
                    openedAt = now.ToString("O"),
                    openingCashAmount = shift.OpeningCashAmount,
                    operatingDay = operatingDay.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
                }),
                Metadata: JsonSerializer.Serialize(new
                {
                    cashDrawerId = shift.CashDrawerId,
                    openingCashAmount = shift.OpeningCashAmount,
                    denominationBreakdown = breakdownJson
                })));

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        return Result<Shift>.Success(shift);
    }

    /// <summary>
    /// Calculates the expected cash balance for the shift (Req 12.8).
    /// Expected = opening + cash_sales(not voided) + deposits - withdrawals - cash_refunds - voided_cash_sales.
    /// </summary>
    public async Task<Result<Money>> GetExpectedCashAsync(Guid shiftId, CancellationToken ct)
    {
        var shift = await _shiftRepository.GetByIdAsync(shiftId, ct);
        if (shift is null)
            return Result<Money>.Failure(ErrorCode.UnexpectedError);

        var expectedCash = await CalculateExpectedCashAsync(shift, ct);
        return Result<Money>.Success(expectedCash);
    }

    /// <summary>
    /// Closes the shift with denomination count and variance calculation (Req 12.9-12.14).
    /// </summary>
    public async Task<Result<ShiftSummary>> CloseAsync(CloseShiftCommand cmd, CancellationToken ct)
    {
        var shift = await _shiftRepository.GetByIdAsync(cmd.ShiftId, ct);
        if (shift is null)
            return Result<ShiftSummary>.Failure(ErrorCode.UnexpectedError);

        // Validate shift is open
        if (shift.Status != "open")
            return Result<ShiftSummary>.Failure(ErrorCode.ShiftAlreadyClosed);

        // Validate user owns this shift
        if (shift.UserId != cmd.UserId)
            return Result<ShiftSummary>.Failure(ErrorCode.InsufficientPermissions);

        // Validate closing amount range (0.00-999999.99)
        if (cmd.ClosingCashAmount < 0m || cmd.ClosingCashAmount > 999999.99m)
            return Result<ShiftSummary>.Failure(ErrorCode.UnexpectedError);

        // Req 12.9: Validate denomination breakdown
        if (cmd.Denominations is null || cmd.Denominations.Count == 0)
            return Result<ShiftSummary>.Failure(ErrorCode.UnexpectedError);

        foreach (var denom in cmd.Denominations)
        {
            var denomResult = Denomination.Create(denom.DenominationValue);
            if (!denomResult.IsSuccess)
                return Result<ShiftSummary>.Failure(ErrorCode.UnexpectedError);

            if (denom.Quantity < 0)
                return Result<ShiftSummary>.Failure(ErrorCode.UnexpectedError);
        }

        // Calculate expected cash (Req 12.8)
        var expectedCash = await CalculateExpectedCashAsync(shift, ct);

        // Req 12.10: Calculate variance
        var closingMoney = new Money(cmd.ClosingCashAmount);
        var variance = closingMoney - expectedCash;
        var varianceStatus = DetermineVarianceStatus(variance);

        // Req 12.11, 12.12: Validate variance notes
        if (Math.Abs(variance.Amount) > 10.00m)
        {
            if (string.IsNullOrWhiteSpace(cmd.VarianceNotes) ||
                cmd.VarianceNotes.Length < 1 ||
                cmd.VarianceNotes.Length > 500)
            {
                return Result<ShiftSummary>.Failure(ErrorCode.VarianceExplanationRequired);
            }
        }

        var now = _clock.UtcNow;

        // Load all data needed for summary
        var transactions = await _transactionRepository.GetByShiftIdAsync(shift.Id, ct);
        var returns = await _returnRepository.GetByShiftIdAsync(shift.Id, ct);
        var movements = await _cashMovementRepository.GetByShiftIdAsync(shift.Id, ct);
        var user = await _userRepository.GetByIdAsync(shift.UserId, ct);

        var summaryMetrics = ComputeSummaryMetrics(transactions, returns, movements);

        // Create closing cash count
        var breakdownJson = SerializeDenominations(cmd.Denominations);
        var closingCashCount = new CashCount
        {
            Id = Guid.NewGuid(),
            ShiftId = shift.Id,
            CountType = "closing",
            TotalAmount = cmd.ClosingCashAmount,
            Breakdown = breakdownJson,
            CountedAt = now,
            CountedBy = cmd.UserId
        };

        // Req 12.13: Record closing data and mark as closed
        var beforeState = JsonSerializer.Serialize(new
        {
            status = shift.Status,
            closedAt = (DateTimeOffset?)null,
            closingCashAmount = (decimal?)null,
            expectedCashBalance = (decimal?)null,
            varianceAmount = (decimal?)null,
            varianceStatus = (string?)null
        });

        shift.Status = "closed";
        shift.ClosedAt = now;
        shift.ClosingCashAmount = cmd.ClosingCashAmount;
        shift.ExpectedCashBalance = expectedCash.Amount; // Req 12: Freeze expected balance
        shift.VarianceAmount = variance.Amount;
        shift.VarianceStatus = varianceStatus;
        shift.VarianceNotes = cmd.VarianceNotes;

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            _shiftRepository.Update(shift);
            await _cashCountRepository.AddRangeAsync(new[] { closingCashCount }, ct);

            // Audit logging
            _auditWriter.Enqueue(new AuditEntryDraft(
                OperationType: "CloseShift",
                EntityType: "Shift",
                EntityId: shift.Id,
                RelatedEntityIds: null,
                BeforeState: beforeState,
                AfterState: JsonSerializer.Serialize(new
                {
                    status = shift.Status,
                    closedAt = now.ToString("O"),
                    closingCashAmount = shift.ClosingCashAmount,
                    expectedCashBalance = shift.ExpectedCashBalance,
                    varianceAmount = shift.VarianceAmount,
                    varianceStatus = shift.VarianceStatus,
                    varianceNotes = shift.VarianceNotes
                }),
                Metadata: JsonSerializer.Serialize(new
                {
                    shiftId = shift.Id,
                    closingCashAmount = cmd.ClosingCashAmount,
                    expectedCashBalance = expectedCash.Amount,
                    varianceAmount = variance.Amount,
                    varianceStatus,
                    transactionCount = summaryMetrics.TransactionCount,
                    cashReturnCount = summaryMetrics.CashReturnCount,
                    voidedTransactionCount = summaryMetrics.VoidedTransactionCount
                })));

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        // Req 12.14: Generate shift summary
        var summary = new ShiftSummary(
            ShiftId: shift.Id,
            UserName: user?.Username ?? string.Empty,
            OpeningCash: new Money(shift.OpeningCashAmount),
            TotalCashSales: summaryMetrics.TotalCashSales,
            TotalCashRefunds: summaryMetrics.TotalCashRefunds,
            TotalVoidedCashSales: summaryMetrics.TotalVoidedCashSales,
            TotalWithdrawals: summaryMetrics.TotalWithdrawals,
            TotalDeposits: summaryMetrics.TotalDeposits,
            ExpectedCash: expectedCash,
            ClosingCash: closingMoney,
            Variance: variance,
            VarianceStatus: varianceStatus,
            TransactionCount: summaryMetrics.TransactionCount,
            CashReturnCount: summaryMetrics.CashReturnCount,
            VoidedTransactionCount: summaryMetrics.VoidedTransactionCount,
            OpenedAt: shift.OpenedAt,
            ClosedAt: now);

        return Result<ShiftSummary>.Success(summary);
    }

    /// <summary>
    /// Records a cash withdrawal or deposit during a shift (Req 12.5-12.7).
    /// </summary>
    public async Task<Result<CashMovement>> RecordMovementAsync(CashMovementCommand cmd, CancellationToken ct)
    {
        // Validate movement type
        if (string.IsNullOrWhiteSpace(cmd.MovementType) || !ValidMovementTypes.Contains(cmd.MovementType))
            return Result<CashMovement>.Failure(ErrorCode.UnexpectedError);

        // Validate amount (0.01-99999.99)
        if (cmd.Amount < 0.01m || cmd.Amount > 99999.99m)
            return Result<CashMovement>.Failure(ErrorCode.UnexpectedError);

        // Validate reason (required, 1-200 chars)
        if (string.IsNullOrWhiteSpace(cmd.Reason) || cmd.Reason.Length > 200)
            return Result<CashMovement>.Failure(ErrorCode.UnexpectedError);

        // Validate optional notes (0-200 chars)
        if (cmd.Notes is not null && cmd.Notes.Length > 200)
            return Result<CashMovement>.Failure(ErrorCode.UnexpectedError);

        // Validate shift is open
        var shift = await _shiftRepository.GetByIdAsync(cmd.ShiftId, ct);
        if (shift is null)
            return Result<CashMovement>.Failure(ErrorCode.UnexpectedError);

        if (shift.Status != "open")
            return Result<CashMovement>.Failure(ErrorCode.ShiftAlreadyClosed);

        var now = _clock.UtcNow;

        var movement = new CashMovement
        {
            Id = Guid.NewGuid(),
            ShiftId = cmd.ShiftId,
            MovementType = cmd.MovementType.ToLowerInvariant(),
            Amount = cmd.Amount,
            Reason = cmd.Reason,
            Notes = cmd.Notes,
            UserId = cmd.UserId,
            OccurredAt = now
        };

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _cashMovementRepository.AddAsync(movement, ct);

            // Req 12.7: Log in AuditLog with timestamp, user id, shift id, operation, amount, reason
            _auditWriter.Enqueue(new AuditEntryDraft(
                OperationType: movement.MovementType == "withdrawal" ? "CashWithdrawal" : "CashDeposit",
                EntityType: "CashMovement",
                EntityId: movement.Id,
                RelatedEntityIds: new List<Guid> { cmd.ShiftId },
                BeforeState: null,
                AfterState: JsonSerializer.Serialize(new
                {
                    movementId = movement.Id,
                    shiftId = movement.ShiftId,
                    movementType = movement.MovementType,
                    amount = movement.Amount,
                    reason = movement.Reason,
                    notes = movement.Notes,
                    userId = movement.UserId,
                    occurredAt = now.ToString("O")
                }),
                Metadata: JsonSerializer.Serialize(new
                {
                    shiftId = movement.ShiftId,
                    movementType = movement.MovementType,
                    amount = movement.Amount,
                    reason = movement.Reason
                })));

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        return Result<CashMovement>.Success(movement);
    }

    #region Private Helpers

    /// <summary>
    /// Calculates expected cash balance for a shift (Req 12.8):
    /// opening + cash_sales(not voided) + deposits - withdrawals - cash_refunds - voided_cash_sales
    /// </summary>
    private async Task<Money> CalculateExpectedCashAsync(Shift shift, CancellationToken ct)
    {
        var transactions = await _transactionRepository.GetByShiftIdAsync(shift.Id, ct);
        var returns = await _returnRepository.GetByShiftIdAsync(shift.Id, ct);
        var movements = await _cashMovementRepository.GetByShiftIdAsync(shift.Id, ct);

        var opening = new Money(shift.OpeningCashAmount);

        // Cash sales from non-voided transactions
        var cashSales = transactions
            .Where(t => !t.IsVoided)
            .SelectMany(t => t.Payments)
            .Where(p => p.Method == "cash")
            .Sum(p => p.Amount);

        // Voided cash sales (for deduction)
        var voidedCashSales = transactions
            .Where(t => t.IsVoided)
            .SelectMany(t => t.Payments)
            .Where(p => p.Method == "cash")
            .Sum(p => p.Amount);

        // Cash refunds from returns in this shift
        var cashRefunds = returns
            .Where(r => r.RefundMethod == "cash")
            .Sum(r => r.RefundAmount);

        // Deposits
        var deposits = movements
            .Where(m => m.MovementType == "deposit")
            .Sum(m => m.Amount);

        // Withdrawals
        var withdrawals = movements
            .Where(m => m.MovementType == "withdrawal")
            .Sum(m => m.Amount);

        var expected = opening.Amount + cashSales + deposits - withdrawals - cashRefunds - voidedCashSales;
        return new Money(expected);
    }

    private static string DetermineVarianceStatus(Money variance)
    {
        if (variance.Amount > 0m) return "over";
        if (variance.Amount < 0m) return "short";
        return "balanced";
    }

    private static string SerializeDenominations(IReadOnlyList<DenominationCount> denominations)
    {
        return JsonSerializer.Serialize(denominations.Select(d => new
        {
            denomination = d.DenominationValue,
            quantity = d.Quantity,
            subtotal = d.DenominationValue * d.Quantity
        }));
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

    private static SummaryMetrics ComputeSummaryMetrics(
        IReadOnlyList<Transaction> transactions,
        IReadOnlyList<Return> returns,
        IReadOnlyList<CashMovement> movements)
    {
        // Non-voided transaction cash sales
        var totalCashSales = new Money(transactions
            .Where(t => !t.IsVoided)
            .SelectMany(t => t.Payments)
            .Where(p => p.Method == "cash")
            .Sum(p => p.Amount));

        // Voided cash sales
        var totalVoidedCashSales = new Money(transactions
            .Where(t => t.IsVoided)
            .SelectMany(t => t.Payments)
            .Where(p => p.Method == "cash")
            .Sum(p => p.Amount));

        // Cash refunds
        var totalCashRefunds = new Money(returns
            .Where(r => r.RefundMethod == "cash")
            .Sum(r => r.RefundAmount));

        // Deposits and withdrawals
        var totalDeposits = new Money(movements
            .Where(m => m.MovementType == "deposit")
            .Sum(m => m.Amount));

        var totalWithdrawals = new Money(movements
            .Where(m => m.MovementType == "withdrawal")
            .Sum(m => m.Amount));

        // Counts
        var transactionCount = transactions.Count(t => !t.IsVoided);
        var cashReturnCount = returns.Count(r => r.RefundMethod == "cash");
        var voidedTransactionCount = transactions.Count(t => t.IsVoided);

        return new SummaryMetrics(
            totalCashSales,
            totalCashRefunds,
            totalVoidedCashSales,
            totalWithdrawals,
            totalDeposits,
            transactionCount,
            cashReturnCount,
            voidedTransactionCount);
    }

    private sealed record SummaryMetrics(
        Money TotalCashSales,
        Money TotalCashRefunds,
        Money TotalVoidedCashSales,
        Money TotalWithdrawals,
        Money TotalDeposits,
        int TransactionCount,
        int CashReturnCount,
        int VoidedTransactionCount);

    #endregion
}
