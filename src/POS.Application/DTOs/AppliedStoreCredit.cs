using POS.Domain.ValueObjects;

namespace POS.Application.DTOs;

/// <summary>
/// Result of a successful store credit consumption.
/// </summary>
public sealed record AppliedStoreCredit(
    Money AmountApplied,
    Money RemainingBalance);
