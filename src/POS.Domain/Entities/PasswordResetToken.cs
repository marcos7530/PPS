namespace POS.Domain.Entities;

/// <summary>
/// Represents a password reset token with 24-hour expiry.
/// </summary>
public class PasswordResetToken
{
    [Key]
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [Required, MaxLength(32)]
    public byte[] TokenHash { get; set; } = Array.Empty<byte>();

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Token expires 24 hours after creation.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }

    public DateTimeOffset? InvalidatedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
    public bool IsConsumed => ConsumedAt.HasValue;
    public bool IsInvalidated => InvalidatedAt.HasValue;
    public bool IsUsable(DateTimeOffset now) => !IsExpired(now) && !IsConsumed && !IsInvalidated;
}
