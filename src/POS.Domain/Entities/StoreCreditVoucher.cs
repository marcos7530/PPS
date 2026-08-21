namespace POS.Domain.Entities;

/// <summary>
/// Represents a store credit voucher with a 32-character code and 365-day expiry.
/// </summary>
public class StoreCreditVoucher
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// 32-character unique code.
    /// </summary>
    [Required, MaxLength(32)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Voucher amount. Must be > 0.
    /// </summary>
    [Column(TypeName = "decimal(9,2)")]
    public decimal Amount { get; set; }

    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>
    /// Expires 365 days after issuance.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Allowed: unused, used. Default: unused.
    /// </summary>
    [Required, MaxLength(10)]
    public string Status { get; set; } = "unused";

    public DateTimeOffset? UsedAt { get; set; }

    public Guid? UsedInTransactionId { get; set; }

    public Guid? OriginReturnId { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UsedInTransactionId))]
    public Transaction? UsedInTransaction { get; set; }

    [ForeignKey(nameof(OriginReturnId))]
    public Return? OriginReturn { get; set; }

    // Domain logic
    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
    public bool IsUsed => Status == "used";
    public bool IsUsable(DateTimeOffset now) => !IsExpired(now) && !IsUsed;
}
