namespace POS.Domain.Entities;

/// <summary>
/// Pre-aggregated daily sales data by operating day, category, and product.
/// Composite PK: (OperatingDay, CategoryId, ProductId).
/// </summary>
public class DailySalesAggregate
{
    public DateOnly OperatingDay { get; set; }

    public Guid CategoryId { get; set; }

    public Guid ProductId { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal NetSalesAmount { get; set; }

    public int TransactionCount { get; set; }

    public int QuantitySold { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal GrossMarginAmount { get; set; }

    public DateTimeOffset RefreshedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(CategoryId))]
    public Category Category { get; set; } = null!;

    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;
}
