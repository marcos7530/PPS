namespace POS.Domain.Entities;

/// <summary>
/// Represents a receipt for a transaction or return.
/// CHECK: Exactly one of TransactionId or ReturnId must be non-null.
/// </summary>
public class Receipt
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// FK to Transaction (mutually exclusive with ReturnId).
    /// </summary>
    public Guid? TransactionId { get; set; }

    /// <summary>
    /// FK to Return (mutually exclusive with TransactionId).
    /// </summary>
    public Guid? ReturnId { get; set; }

    /// <summary>
    /// Number of times this receipt has been reprinted.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int ReprintCount { get; set; }

    public DateTimeOffset FirstEmittedAt { get; set; }

    /// <summary>
    /// Allowed: thermal_printer, pdf, email.
    /// </summary>
    [Required, MaxLength(20)]
    public string LastChannel { get; set; } = string.Empty;

    /// <summary>
    /// JSON payload snapshot of the receipt content.
    /// </summary>
    [Required]
    public string PayloadSnapshot { get; set; } = string.Empty;

    // Navigation properties
    [ForeignKey(nameof(TransactionId))]
    public Transaction? Transaction { get; set; }

    [ForeignKey(nameof(ReturnId))]
    public Return? Return { get; set; }
}
