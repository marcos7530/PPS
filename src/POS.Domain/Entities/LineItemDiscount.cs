namespace POS.Domain.Entities;

/// <summary>
/// Represents a discount applied to a single transaction line item. UNIQUE on LineItemId.
/// </summary>
public class LineItemDiscount
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// One discount per line item (UNIQUE constraint).
    /// </summary>
    public Guid LineItemId { get; set; }

    /// <summary>
    /// Allowed: percentage, fixed.
    /// </summary>
    [Required, MaxLength(15)]
    public string DiscountType { get; set; } = string.Empty;

    /// <summary>
    /// Percentage value (0-100) when DiscountType = percentage.
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? Percentage { get; set; }

    /// <summary>
    /// Computed discount amount. Must be >= 0.
    /// </summary>
    [Column(TypeName = "decimal(9,2)")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Allowed: promotion, frequent_customer, damaged_product, management_authorization, other.
    /// </summary>
    [Required, MaxLength(30)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Notes { get; set; }

    public Guid AppliedBy { get; set; }

    /// <summary>
    /// Manager authorization when discount exceeds cashier limit.
    /// </summary>
    public Guid? AuthorizedBy { get; set; }

    // Navigation properties
    [ForeignKey(nameof(LineItemId))]
    public TransactionLineItem LineItem { get; set; } = null!;

    [ForeignKey(nameof(AppliedBy))]
    public User AppliedByUser { get; set; } = null!;

    [ForeignKey(nameof(AuthorizedBy))]
    public User? AuthorizedByUser { get; set; }
}
