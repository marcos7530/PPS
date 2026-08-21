namespace POS.Domain.Entities;

/// <summary>
/// Represents a completed sales transaction.
/// CHECK: final_amount = subtotal + tax_amount - discount_amount.
/// </summary>
public class Transaction
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Sequential, unique transaction number.
    /// </summary>
    public long TransactionNumber { get; set; }

    public DateTimeOffset CompletedAt { get; set; }

    public DateOnly OperatingDay { get; set; }

    public Guid UserId { get; set; }

    public Guid? ShiftId { get; set; }

    public Guid? CustomerId { get; set; }

    [Column(TypeName = "decimal(9,2)")]
    public decimal Subtotal { get; set; }

    [Column(TypeName = "decimal(9,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(9,2)")]
    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// CHECK: FinalAmount = Subtotal + TaxAmount - DiscountAmount.
    /// </summary>
    [Column(TypeName = "decimal(9,2)")]
    public decimal FinalAmount { get; set; }

    /// <summary>
    /// Must be >= FinalAmount.
    /// </summary>
    [Column(TypeName = "decimal(9,2)")]
    public decimal AmountReceived { get; set; }

    [Column(TypeName = "decimal(9,2)")]
    public decimal ChangeDue { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal TaxRateApplied { get; set; }

    public Guid? DiscountAuthorizedBy { get; set; }

    public bool IsVoided { get; set; }

    public DateTimeOffset? VoidedAt { get; set; }

    public Guid? VoidedBy { get; set; }

    /// <summary>
    /// Allowed: cashier_error, customer_cancellation, pricing_error, duplicate_transaction, other.
    /// </summary>
    [MaxLength(30)]
    public string? VoidReason { get; set; }

    /// <summary>
    /// Required when voided. 1-500 characters.
    /// </summary>
    [MaxLength(500)]
    public string? VoidNotes { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(ShiftId))]
    public Shift? Shift { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public Customer? Customer { get; set; }

    [ForeignKey(nameof(DiscountAuthorizedBy))]
    public User? DiscountAuthorizedByUser { get; set; }

    [ForeignKey(nameof(VoidedBy))]
    public User? VoidedByUser { get; set; }

    public ICollection<TransactionLineItem> LineItems { get; set; } = new List<TransactionLineItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public TransactionDiscount? TransactionDiscount { get; set; }
    public ICollection<Return> Returns { get; set; } = new List<Return>();
    public Receipt? Receipt { get; set; }
}
