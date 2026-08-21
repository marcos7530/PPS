using System.Diagnostics.CodeAnalysis;

namespace POS.Domain.Entities;

/// <summary>
/// Represents a return/refund for a previously completed transaction.
/// </summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
    Justification = "Return is the correct domain term for this entity.")]
public class Return
{
    [Key]
    public Guid Id { get; set; }

    public Guid OriginalTransactionId { get; set; }

    public DateTimeOffset CompletedAt { get; set; }

    public DateOnly OperatingDay { get; set; }

    public Guid UserId { get; set; }

    /// <summary>
    /// Required when refund method is cash.
    /// </summary>
    public Guid? ShiftId { get; set; }

    /// <summary>
    /// Total refund amount. Must be > 0.
    /// </summary>
    [Column(TypeName = "decimal(9,2)")]
    public decimal RefundAmount { get; set; }

    /// <summary>
    /// Allowed: cash, credit_card_reversal, store_credit.
    /// </summary>
    [Required, MaxLength(25)]
    public string RefundMethod { get; set; } = string.Empty;

    /// <summary>
    /// Allowed: defective_product, customer_regret, wrong_product, other.
    /// </summary>
    [Required, MaxLength(25)]
    public string ReasonCode { get; set; } = string.Empty;

    /// <summary>
    /// Required if refund method = store_credit or refund amount > 500.
    /// </summary>
    public Guid? AuthorizedBy { get; set; }

    // Navigation properties
    [ForeignKey(nameof(OriginalTransactionId))]
    public Transaction OriginalTransaction { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(ShiftId))]
    public Shift? Shift { get; set; }

    [ForeignKey(nameof(AuthorizedBy))]
    public User? AuthorizedByUser { get; set; }

    public ICollection<ReturnLineItem> LineItems { get; set; } = new List<ReturnLineItem>();
    public Receipt? Receipt { get; set; }
}
