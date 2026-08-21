namespace POS.Domain.Entities;

/// <summary>
/// Represents an active user session with token-based authentication.
/// </summary>
public class Session
{
    [Key]
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [Required, MaxLength(32)]
    public byte[] TokenHash { get; set; } = Array.Empty<byte>();

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Session expires 8 hours after creation.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [MaxLength(400)]
    public string? UserAgent { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive(DateTimeOffset now) => !IsExpired(now) && !IsRevoked;
}
