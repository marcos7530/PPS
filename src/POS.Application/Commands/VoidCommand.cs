namespace POS.Application.Commands;

/// <summary>
/// Command to void a transaction.
/// </summary>
public sealed record VoidCommand(
    Guid TransactionId,
    Guid VoidedBy,
    string VoidReason,
    string VoidNotes,
    Guid? AuthorizedBy);
