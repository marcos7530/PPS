namespace POS.Domain.Entities;

/// <summary>
/// Single-row system configuration (id = 1).
/// </summary>
public class SystemConfiguration
{
    /// <summary>
    /// Always 1 (single-row table).
    /// </summary>
    [Key]
    public short Id { get; set; } = 1;

    [Required, MaxLength(100)]
    public string BusinessName { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string BusinessAddress { get; set; } = string.Empty;

    /// <summary>
    /// Tax rate (0-100%).
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    [Range(0, 100)]
    public decimal TaxRatePercentage { get; set; }

    /// <summary>
    /// ISO 4217 currency code (3 characters).
    /// </summary>
    [Required, MaxLength(3), MinLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>
    /// IANA timezone identifier. Default: America/Argentina/Buenos_Aires.
    /// </summary>
    [Required, MaxLength(60)]
    public string BusinessTimeZone { get; set; } = "America/Argentina/Buenos_Aires";

    /// <summary>
    /// Global profit margin percentage (0-1000). Default: 30.
    /// </summary>
    [Column(TypeName = "decimal(7,2)")]
    [Range(0, 1000)]
    public decimal GlobalProfitMarginPercentage { get; set; } = 30m;

    /// <summary>
    /// Maximum discount percentage a cashier can apply without authorization (0-100). Default: 10.
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    [Range(0, 100)]
    public decimal CashierDiscountLimitPercentage { get; set; } = 10m;

    [MaxLength(200)]
    public string? ReceiptFooterText { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid UpdatedBy { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UpdatedBy))]
    public User UpdatedByUser { get; set; } = null!;
}
