namespace POS.Application.Commands;

/// <summary>
/// Command to create a new customer (Req 13.1).
/// </summary>
public sealed record CreateCustomerCommand(
    string Name,
    string? Email,
    string? Phone,
    string? Notes,
    Guid PerformedBy,
    bool ConfirmPhoneDuplicate = false);
