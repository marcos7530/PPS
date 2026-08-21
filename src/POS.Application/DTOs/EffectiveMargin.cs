using POS.Domain.ValueObjects;

namespace POS.Application.DTOs;

/// <summary>
/// Resolved effective margin for a product (product > category ancestor > global).
/// </summary>
public sealed record EffectiveMargin(
    Percentage Margin,
    string Source);
