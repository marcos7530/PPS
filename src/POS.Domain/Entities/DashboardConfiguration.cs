namespace POS.Domain.Entities;

/// <summary>
/// Represents a user's dashboard widget configuration. UNIQUE on UserId.
/// </summary>
public class DashboardConfiguration
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// One configuration per user (UNIQUE constraint).
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// JSON array of up to 8 widget configurations.
    /// </summary>
    [Required]
    public string Widgets { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
