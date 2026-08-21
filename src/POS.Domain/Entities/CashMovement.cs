namespace POS.Domain.Entities;

/// <summary>
/// Represents a cash withdrawal or deposit during an open shift.
/// </summary>
public class CashMovement
{
    [Key]
    public Guid Id { get; set; }

    public Guid ShiftId { get; set; }

    /// <summary>
    /// Allowed: withdrawal, deposit.
    /// </summary>
    [Required, MaxLength(15)]
    public string MovementType { get; set; } = string.Empty;

    /// <summary>
    /// Amount of the movement. Between 0.01 and 99,999.99.
    /// </summary>
    [Column(TypeName = "decimal(9,2)")]
    [Range(0.01, 99999.99)]
    public decimal Amount { get; set; }

    [Required, MaxLength(200)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Notes { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(ShiftId))]
    public Shift Shift { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
