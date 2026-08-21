namespace POS.Application.DTOs;

/// <summary>
/// Source entity for receipt emission (transaction or return).
/// </summary>
public sealed record ReceiptSource(
    Guid EntityId,
    ReceiptSourceType Type);

public enum ReceiptSourceType
{
    Transaction,
    Return
}
