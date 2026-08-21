using POS.Domain.ValueObjects;

namespace POS.Application.Views;

/// <summary>
/// View of a completed return operation.
/// </summary>
public sealed record CompletedReturn(
    Guid ReturnId,
    Guid OriginalTransactionId,
    Money RefundAmount,
    string RefundMethod,
    string? VoucherCode,
    DateTimeOffset CompletedAt);
