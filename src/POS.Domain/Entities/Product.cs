namespace POS.Domain.Entities;

/// <summary>
/// Represents a product in the POS system with pricing, stock, and margin management.
/// </summary>
public class Product
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(50)]
    public string Sku { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(48)]
    public string? BarcodeValue { get; set; }

    [MaxLength(10)]
    public string? BarcodeFormat { get; set; }

    public Guid CategoryId { get; set; }

    [Column(TypeName = "decimal(9,2)")]
    [Range(0.01, 999999.99)]
    public decimal SalePrice { get; set; }

    [Column(TypeName = "decimal(9,2)")]
    [Range(0.01, 999999.99)]
    public decimal CostPrice { get; set; }

    /// <summary>
    /// Profit margin percentage (0-1000). Null if not overridden.
    /// </summary>
    [Column(TypeName = "decimal(7,2)")]
    public decimal? ProfitMarginPercentage { get; set; }

    public bool IsPriceManuallyOverridden { get; set; }

    public Guid? PriceOverrideBy { get; set; }

    public DateTimeOffset? PriceOverrideAt { get; set; }

    [Range(0, 999999)]
    public int Quantity { get; set; }

    [Range(0, 999999)]
    public int MinStockThreshold { get; set; }

    public bool IsDeactivated { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Computed property
    [NotMapped]
    public bool IsLowStock => Quantity <= MinStockThreshold;

    // Navigation properties
    [ForeignKey(nameof(CategoryId))]
    public Category Category { get; set; } = null!;

    [ForeignKey(nameof(PriceOverrideBy))]
    public User? PriceOverrideByUser { get; set; }

    public ProductImage? Image { get; set; }
}
