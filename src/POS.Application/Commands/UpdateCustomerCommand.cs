namespace POS.Application.Commands;

/// <summary>
/// Command to update an existing customer's details (Req 13.11).
/// </summary>
public sealed record UpdateCustomerCommand(
    Guid CustomerId,
    string Name,
    string? Email,
    string? Phone,
    string? Notes,
    Guid PerformedBy,
    bool ConfirmPhoneDuplicate = false);
