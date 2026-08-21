namespace POS.Domain.Entities;

/// <summary>
/// Represents a customer's store credit balance. One per customer (UNIQUE on CustomerId).
/// </summary>
public class StoreCredit
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// One-to-one with Customer (UNIQUE constraint).
    /// </summary>
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Current balance. Must be >= 0.
    /// </summary>
    [Column(TypeName = "decimal(9,2)")]
    [Range(0, double.MaxValue)]
    public decimal Balance { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(CustomerId))]
    public Customer Customer { get; set; } = null!;
}
