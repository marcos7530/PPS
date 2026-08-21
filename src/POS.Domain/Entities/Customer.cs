namespace POS.Domain.Entities;

/// <summary>
/// Represents a customer with optional email (UNIQUE, case-insensitive) and phone.
/// </summary>
public class Customer
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Unique case-insensitive email. Optional.
    /// </summary>
    [MaxLength(100)]
    public string? Email { get; set; }

    /// <summary>
    /// Phone number (7-20 characters).
    /// </summary>
    [MaxLength(20)]
    public string? Phone { get; set; }

    /// <summary>
    /// Normalized phone for duplicate detection.
    /// </summary>
    [MaxLength(20)]
    public string? PhoneNormalized { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid CreatedBy { get; set; }

    // Navigation properties
    [ForeignKey(nameof(CreatedBy))]
    public User CreatedByUser { get; set; } = null!;

    public StoreCredit? StoreCredit { get; set; }
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
