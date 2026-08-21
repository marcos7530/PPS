namespace POS.Domain.Entities;

/// <summary>
/// Represents a line item in a return. UNIQUE (ReturnId, LineItemId).
/// </summary>
public class ReturnLineItem
{
    [Key]
    public Guid Id { get; set; }

    public Guid ReturnId { get; set; }

    public Guid LineItemId { get; set; }

    public Guid ProductId { get; set; }

    [Range(1, 9999)]
    public int ReturnQuantity { get; set; }

    [Column(TypeName = "decimal(9,2)")]
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Computed: ReturnQuantity * UnitPrice.
    /// </summary>
    [Column(TypeName = "decimal(9,2)")]
    public decimal LineRefundAmount { get; set; }

    // Navigation properties
    [ForeignKey(nameof(ReturnId))]
    public Return Return { get; set; } = null!;

    [ForeignKey(nameof(LineItemId))]
    public TransactionLineItem OriginalLineItem { get; set; } = null!;

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;
}
