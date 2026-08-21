namespace POS.Domain.Entities;

/// <summary>
/// Represents a product image. One image per product (UNIQUE constraint on ProductId).
/// Max file size: 5 MB (5,242,880 bytes). Supported formats: JPEG, PNG, WebP.
/// </summary>
public class ProductImage
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// One-to-one with Product (UNIQUE constraint).
    /// </summary>
    public Guid ProductId { get; set; }

    [Required, MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// Allowed values: image/jpeg, image/png, image/webp.
    /// </summary>
    [Required, MaxLength(20)]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes. Max 5,242,880.
    /// </summary>
    [Range(1, 5_242_880)]
    public int ByteSize { get; set; }

    [Range(1, 4000)]
    public int WidthPx { get; set; }

    [Range(1, 4000)]
    public int HeightPx { get; set; }

    [Required, MaxLength(400)]
    public string StoragePath { get; set; } = string.Empty;

    [Required, MaxLength(400)]
    public string ThumbnailPath { get; set; } = string.Empty;

    public Guid UploadedBy { get; set; }

    public DateTimeOffset UploadedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;

    [ForeignKey(nameof(UploadedBy))]
    public User UploadedByUser { get; set; } = null!;
}
