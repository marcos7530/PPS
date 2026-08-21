namespace POS.Application.Commands;

/// <summary>
/// Command to complete a return/refund for previously sold items.
/// </summary>
public sealed record CompleteReturnCommand(
    Guid OriginalTransactionId,
    Guid UserId,
    IReadOnlyList<ReturnLineDetail> Lines,
    string RefundMethod,
    string ReasonCode,
    Guid? AuthorizedBy);

/// <summary>
/// Detail of a line item being returned.
/// </summary>
public sealed record ReturnLineDetail(
    Guid OriginalLineItemId,
    int Quantity);
