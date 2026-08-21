namespace POS.Domain.Entities;

/// <summary>
/// Represents a cash count (opening or closing) for a shift.
/// UNIQUE (ShiftId, CountType).
/// </summary>
public class CashCount
{
    [Key]
    public Guid Id { get; set; }

    public Guid ShiftId { get; set; }

    /// <summary>
    /// Allowed: opening, closing.
    /// </summary>
    [Required, MaxLength(10)]
    public string CountType { get; set; } = string.Empty;

    /// <summary>
    /// Total counted amount (0-999,999.99).
    /// </summary>
    [Column(TypeName = "decimal(9,2)")]
    [Range(0, 999999.99)]
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// JSON string representing denomination breakdown (10 denominations).
    /// </summary>
    [Required]
    public string Breakdown { get; set; } = string.Empty;

    public DateTimeOffset CountedAt { get; set; }

    public Guid CountedBy { get; set; }

    // Navigation properties
    [ForeignKey(nameof(ShiftId))]
    public Shift Shift { get; set; } = null!;

    [ForeignKey(nameof(CountedBy))]
    public User CountedByUser { get; set; } = null!;
}
