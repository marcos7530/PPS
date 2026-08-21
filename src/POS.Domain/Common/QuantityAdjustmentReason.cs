namespace POS.Domain.Common;

/// <summary>
/// Reasons for manual product quantity adjustments (Req 10.6).
/// </summary>
public enum QuantityAdjustmentReason
{
    Return,
    Damage,
    Correction,
    Restock
}
