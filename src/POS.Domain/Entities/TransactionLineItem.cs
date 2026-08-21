namespace POS.Domain.Entities;

/// <summary>
/// Represents a line item in a transaction. UNIQUE (TransactionId, ProductId) - scanning increments quantity.
/// </summary>
public class TransactionLineItem
{
    [Key]
    public Guid Id { get; set; }

    public Guid TransactionId { get; set; }

    public Guid ProductId { get; set; }

    /// <summary>
    /// Snapshot of product name at time of transaction.
    /// </summary>
    [Required, MaxLength(100)]
    public string ProductNameSnapshot { get; set; } = string.Empty;

    [Range(1, 9999)]
    public int Quantity { get; set; }

    [Column(TypeName = "decimal(9,2)")]
    [Range(0.01, 999999.99)]
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Recorded cost price at time of sale for margin calculations.
    /// </summary>
    [Column(TypeName = "decimal(9,2)")]
    public decimal? RecordedCostPrice { get; set; }

    [Column(TypeName = "decimal(9,2)")]
    public decimal LineDiscountAmount { get; set; }

    /// <summary>
    /// Computed: Quantity * UnitPrice - LineDiscountAmount.
    /// </summary>
    [Column(TypeName = "decimal(9,2)")]
    public decimal LineAmount { get; set; }

    /// <summary>
    /// Number of items returned from this line. Must be between 0 and Quantity.
    /// </summary>
    [Range(0, 9999)]
    public int ReturnedQuantity { get; set; }

    // Navigation properties
    [ForeignKey(nameof(TransactionId))]
    public Transaction Transaction { get; set; } = null!;

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;

    public LineItemDiscount? Discount { get; set; }
    public ICollection<ReturnLineItem> ReturnLineItems { get; set; } = new List<ReturnLineItem>();
}
