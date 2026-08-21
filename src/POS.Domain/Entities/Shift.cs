namespace POS.Domain.Entities;

/// <summary>
/// Represents a cash register shift with opening/closing amounts and variance tracking.
/// </summary>
public class Shift
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(20)]
    public string CashDrawerId { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public DateTimeOffset OpenedAt { get; set; }

    public DateOnly OperatingDay { get; set; }

    [Column(TypeName = "decimal(9,2)")]
    [Range(0, 999999.99)]
    public decimal OpeningCashAmount { get; set; }

    /// <summary>
    /// Allowed: open, closed.
    /// </summary>
    [Required, MaxLength(10)]
    public string Status { get; set; } = "open";

    public DateTimeOffset? ClosedAt { get; set; }

    [Column(TypeName = "decimal(9,2)")]
    public decimal? ClosingCashAmount { get; set; }

    /// <summary>
    /// Expected cash balance frozen at close time.
    /// </summary>
    [Column(TypeName = "decimal(9,2)")]
    public decimal? ExpectedCashBalance { get; set; }

    /// <summary>
    /// Variance = ClosingCashAmount - ExpectedCashBalance.
    /// </summary>
    [Column(TypeName = "decimal(9,2)")]
    public decimal? VarianceAmount { get; set; }

    /// <summary>
    /// Allowed: over, short, balanced.
    /// </summary>
    [MaxLength(10)]
    public string? VarianceStatus { get; set; }

    /// <summary>
    /// Required when |VarianceAmount| > 10.
    /// </summary>
    [MaxLength(500)]
    public string? VarianceNotes { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    public ICollection<CashMovement> CashMovements { get; set; } = new List<CashMovement>();
    public ICollection<CashCount> CashCounts { get; set; } = new List<CashCount>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Return> Returns { get; set; } = new List<Return>();
}
