namespace POS.Domain.Entities;

/// <summary>
/// Represents a payment applied to a transaction.
/// Supports split payments across multiple methods.
/// </summary>
public class Payment
{
    [Key]
    public Guid Id { get; set; }

    public Guid TransactionId { get; set; }

    /// <summary>
    /// Allowed: cash, credit_card, debit_card, store_credit.
    /// </summary>
    [Required, MaxLength(20)]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Amount paid. Must be > 0.
    /// </summary>
    [Column(TypeName = "decimal(9,2)")]
    public decimal Amount { get; set; }

    /// <summary>
    /// FK to StoreCreditVoucher when method = store_credit and using a voucher.
    /// </summary>
    public Guid? VoucherId { get; set; }

    /// <summary>
    /// FK to Customer when method = store_credit and using balance.
    /// </summary>
    public Guid? StoreCreditCustomerId { get; set; }

    /// <summary>
    /// Whether this payment is currently active (not reversed).
    /// </summary>
    public bool IsConsumptionActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(TransactionId))]
    public Transaction Transaction { get; set; } = null!;

    [ForeignKey(nameof(VoucherId))]
    public StoreCreditVoucher? Voucher { get; set; }

    [ForeignKey(nameof(StoreCreditCustomerId))]
    public Customer? StoreCreditCustomer { get; set; }
}
