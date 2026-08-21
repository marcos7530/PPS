using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Repositories;
using POS.Application.Interfaces.Services;
using POS.Domain.Common;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Application.Services;

/// <summary>
/// Applies line-item and transaction-level discounts with role-based authorization (Req 19.1-19.18).
/// </summary>
public sealed class DiscountService
{
    /// <summary>
    /// Predefined discount reasons (Req 19.14).
    /// </summary>
    private static readonly HashSet<string> ValidReasons = new(StringComparer.OrdinalIgnoreCase)
    {
        "promotion",
        "frequent_customer",
        "damaged_product",
        "management_authorization",
        "other"
    };

    private readonly ISystemConfigurationRepository _configRepository;
    private readonly IUserRepository _userRepository;
    private readonly IElevationService _elevationService;
    private readonly IAuditWriter _auditWriter;

    public DiscountService(
        ISystemConfigurationRepository configRepository,
        IUserRepository userRepository,
        IElevationService elevationService,
        IAuditWriter auditWriter)
    {
        _configRepository = configRepository;
        _userRepository = userRepository;
        _elevationService = elevationService;
        _auditWriter = auditWriter;
    }

    /// <summary>
    /// Result of applying a discount, including a below-cost warning flag (Req 19.15-19.16).
    /// </summary>
    public sealed record DiscountResult(
        LineItemDiscount? LineDiscount,
        TransactionDiscount? TransactionDiscount,
        bool BelowCostWarning);

    /// <summary>
    /// Applies a percentage or fixed discount to a line item (Req 19.1-19.3).
    /// </summary>
    public async Task<Result<DiscountResult>> ApplyLineItemDiscountAsync(
        Guid lineItemId,
        string discountType,
        decimal? percentage,
        decimal? fixedAmount,
        string reason,
        string? notes,
        Guid appliedByUserId,
        decimal lineAmount,
        decimal? recordedCostPrice,
        int quantity,
        ElevationRequest? elevationRequest,
        CancellationToken ct)
    {
        // Validate reason (Req 19.14)
        if (string.IsNullOrWhiteSpace(reason) || !ValidReasons.Contains(reason))
            return Result<DiscountResult>.Failure(ErrorCode.DiscountReasonRequired);

        // Calculate discount amount
        decimal discountAmount;
        decimal? appliedPercentage = null;

        if (discountType == "percentage")
        {
            if (percentage is null or < 0 or > 100)
                return Result<DiscountResult>.Failure(ErrorCode.InvalidDiscountPercentage);

            appliedPercentage = percentage.Value;
            discountAmount = Math.Round(lineAmount * percentage.Value / 100m, 2, MidpointRounding.AwayFromZero);
        }
        else if (discountType == "fixed")
        {
            if (fixedAmount is null or <= 0)
                return Result<DiscountResult>.Failure(ErrorCode.InvalidDiscountPercentage);

            if (fixedAmount.Value > lineAmount)
                return Result<DiscountResult>.Failure(ErrorCode.DiscountAmountExceedsBase);

            discountAmount = Math.Round(fixedAmount.Value, 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            return Result<DiscountResult>.Failure(ErrorCode.InvalidDiscountPercentage);
        }

        // Determine effective percentage for limit checking
        var effectivePercentage = lineAmount > 0
            ? discountAmount / lineAmount * 100m
            : 0m;

        // Check role-based limits (Req 19.8-19.9)
        var authorizationResult = await CheckDiscountLimitAsync(
            appliedByUserId, effectivePercentage, elevationRequest, ct);
        if (!authorizationResult.IsSuccess)
            return Result<DiscountResult>.Failure(authorizationResult.Error!.Value);

        Guid? authorizedBy = authorizationResult.Value;

        // Check below-cost warning (Req 19.15-19.16)
        bool belowCostWarning = false;
        if (recordedCostPrice.HasValue && recordedCostPrice.Value > 0)
        {
            var priceAfterDiscount = lineAmount - discountAmount;
            var totalCost = recordedCostPrice.Value * quantity;
            belowCostWarning = priceAfterDiscount < totalCost;
        }

        var discount = new LineItemDiscount
        {
            Id = Guid.NewGuid(),
            LineItemId = lineItemId,
            DiscountType = discountType,
            Percentage = appliedPercentage,
            Amount = discountAmount,
            Reason = reason,
            Notes = notes,
            AppliedBy = appliedByUserId,
            AuthorizedBy = authorizedBy
        };

        // Audit (Req 19.18)
        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "ApplyLineItemDiscount",
            EntityType: "LineItemDiscount",
            EntityId: discount.Id,
            RelatedEntityIds: new List<Guid> { lineItemId },
            BeforeState: null,
            AfterState: $"{{\"discountType\":\"{discountType}\",\"percentage\":{appliedPercentage?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"},\"amount\":{discountAmount},\"reason\":\"{reason}\",\"authorizedBy\":\"{authorizedBy}\"}}",
            Metadata: belowCostWarning ? "{\"belowCostWarning\":true}" : null));

        return Result<DiscountResult>.Success(
            new DiscountResult(discount, null, belowCostWarning));
    }

    /// <summary>
    /// Applies a percentage or fixed discount at the transaction level (Req 19.4-19.7).
    /// </summary>
    public async Task<Result<DiscountResult>> ApplyTransactionDiscountAsync(
        Guid transactionId,
        string discountType,
        decimal? percentage,
        decimal? fixedAmount,
        string reason,
        string? notes,
        Guid appliedByUserId,
        decimal subtotal,
        decimal currentFinalAmount,
        ElevationRequest? elevationRequest,
        CancellationToken ct)
    {
        // Validate reason (Req 19.14)
        if (string.IsNullOrWhiteSpace(reason) || !ValidReasons.Contains(reason))
            return Result<DiscountResult>.Failure(ErrorCode.DiscountReasonRequired);

        // Calculate discount amount
        decimal discountAmount;
        decimal? appliedPercentage = null;

        if (discountType == "percentage")
        {
            if (percentage is null or < 0 or > 100)
                return Result<DiscountResult>.Failure(ErrorCode.InvalidDiscountPercentage);

            appliedPercentage = percentage.Value;
            discountAmount = Math.Round(subtotal * percentage.Value / 100m, 2, MidpointRounding.AwayFromZero);
        }
        else if (discountType == "fixed")
        {
            if (fixedAmount is null or <= 0)
                return Result<DiscountResult>.Failure(ErrorCode.InvalidDiscountPercentage);

            if (fixedAmount.Value > subtotal)
                return Result<DiscountResult>.Failure(ErrorCode.DiscountAmountExceedsBase);

            discountAmount = Math.Round(fixedAmount.Value, 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            return Result<DiscountResult>.Failure(ErrorCode.InvalidDiscountPercentage);
        }

        // Enforce final_amount >= 0 (Req 19.7)
        var projectedFinal = currentFinalAmount - discountAmount;
        if (projectedFinal < 0)
            return Result<DiscountResult>.Failure(ErrorCode.DiscountWouldResultInNegativeTotal);

        // Determine effective percentage for limit checking
        var effectivePercentage = subtotal > 0
            ? discountAmount / subtotal * 100m
            : 0m;

        // Check role-based limits (Req 19.8-19.9)
        var authorizationResult = await CheckDiscountLimitAsync(
            appliedByUserId, effectivePercentage, elevationRequest, ct);
        if (!authorizationResult.IsSuccess)
            return Result<DiscountResult>.Failure(authorizationResult.Error!.Value);

        Guid? authorizedBy = authorizationResult.Value;

        var discount = new TransactionDiscount
        {
            Id = Guid.NewGuid(),
            TransactionId = transactionId,
            DiscountType = discountType,
            Percentage = appliedPercentage,
            Amount = discountAmount,
            Reason = reason,
            Notes = notes,
            AppliedBy = appliedByUserId,
            AuthorizedBy = authorizedBy
        };

        // Audit (Req 19.18)
        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "ApplyTransactionDiscount",
            EntityType: "TransactionDiscount",
            EntityId: discount.Id,
            RelatedEntityIds: new List<Guid> { transactionId },
            BeforeState: null,
            AfterState: $"{{\"discountType\":\"{discountType}\",\"percentage\":{appliedPercentage?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"},\"amount\":{discountAmount},\"reason\":\"{reason}\",\"authorizedBy\":\"{authorizedBy}\"}}",
            Metadata: null));

        return Result<DiscountResult>.Success(
            new DiscountResult(null, discount, false));
    }

    /// <summary>
    /// Checks if the user's discount exceeds their role limit and requires elevation (Req 19.8-19.13).
    /// Returns the authorizing user ID if elevation was used, or null if within limits.
    /// </summary>
    private async Task<Result<Guid?>> CheckDiscountLimitAsync(
        Guid userId,
        decimal effectivePercentage,
        ElevationRequest? elevationRequest,
        CancellationToken ct)
    {
        var config = await _configRepository.GetAsync(ct);
        var user = await _userRepository.GetByIdWithRolesAsync(userId, ct);

        if (user is null)
            return Result<Guid?>.Failure(ErrorCode.InsufficientPermissions);

        // Determine max allowed percentage for user's role (Req 19.8-19.9)
        var userRoleIds = user.UserRoles.Select(ur => ur.RoleId).ToHashSet();
        decimal maxAllowed;

        if (userRoleIds.Contains(Role.WellKnown.AdministratorId) ||
            userRoleIds.Contains(Role.WellKnown.ManagerId))
        {
            maxAllowed = 100m; // Manager/Admin can apply up to 100%
        }
        else
        {
            maxAllowed = config.CashierDiscountLimitPercentage;
        }

        if (effectivePercentage <= maxAllowed)
            return Result<Guid?>.Success(null); // Within limit, no authorization needed

        // Exceeds limit - require elevation (Req 19.10-19.13)
        if (elevationRequest is null)
            return Result<Guid?>.Failure(ErrorCode.DiscountExceedsLimit);

        var elevationResult = await _elevationService.AuthorizeAsync(elevationRequest, ct);
        if (!elevationResult.IsSuccess)
            return Result<Guid?>.Failure(elevationResult.Error!.Value);

        return Result<Guid?>.Success(elevationResult.Value!.AuthorizingUserId);
    }
}
