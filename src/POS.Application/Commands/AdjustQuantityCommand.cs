using POS.Domain.Common;

namespace POS.Application.Commands;

/// <summary>
/// Command to adjust product quantity with a reason (Req 10.6).
/// </summary>
public sealed record AdjustQuantityCommand(
    Guid ProductId,
    int NewQuantity,
    QuantityAdjustmentReason Reason);
