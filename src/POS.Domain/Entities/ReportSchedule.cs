namespace POS.Domain.Entities;

/// <summary>
/// Represents a scheduled report configuration.
/// </summary>
public class ReportSchedule
{
    [Key]
    public Guid Id { get; set; }

    public Guid CreatedBy { get; set; }

    /// <summary>
    /// Allowed: sales, inventory, audit, discounts, margins.
    /// </summary>
    [Required, MaxLength(20)]
    public string ReportType { get; set; } = string.Empty;

    /// <summary>
    /// Allowed: daily, weekly, monthly.
    /// </summary>
    [Required, MaxLength(10)]
    public string Frequency { get; set; } = string.Empty;

    /// <summary>
    /// Allowed: pdf, excel.
    /// </summary>
    [Required, MaxLength(10)]
    public string ExportFormat { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of 1-10 email addresses.
    /// </summary>
    [Required]
    public string Recipients { get; set; } = string.Empty;

    /// <summary>
    /// JSON filter configuration.
    /// </summary>
    [Required]
    public string FilterJson { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset? LastRunAt { get; set; }

    [MaxLength(10)]
    public string? LastRunStatus { get; set; }

    // Navigation properties
    [ForeignKey(nameof(CreatedBy))]
    public User CreatedByUser { get; set; } = null!;
}
