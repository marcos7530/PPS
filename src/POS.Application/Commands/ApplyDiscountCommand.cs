namespace POS.Application.Commands;

/// <summary>
/// Command to apply a discount to a line item.
/// </summary>
public sealed record ApplyDiscountCommand(
    Guid TransactionId,
    Guid LineItemId,
    decimal DiscountPercentage,
    string Reason,
    Guid? AuthorizedBy);
