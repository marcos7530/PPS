namespace POS.Domain.Entities;

/// <summary>
/// Represents a product category with hierarchical depth constraint (1-5).
/// </summary>
public class Category
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public Guid? ParentCategoryId { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(1, 9999)]
    public int DisplayOrder { get; set; } = 1;

    /// <summary>
    /// Profit margin percentage for this category (0-1000, 2 decimals). Null means inherit from global.
    /// </summary>
    [Column(TypeName = "decimal(7,2)")]
    public decimal? ProfitMarginPercentage { get; set; }

    /// <summary>
    /// Depth in the hierarchy (1 = root, max 5).
    /// </summary>
    [Range(1, 5)]
    public short Depth { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(ParentCategoryId))]
    public Category? ParentCategory { get; set; }

    public ICollection<Category> ChildCategories { get; set; } = new List<Category>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<CategoryClosure> AncestorClosures { get; set; } = new List<CategoryClosure>();
    public ICollection<CategoryClosure> DescendantClosures { get; set; } = new List<CategoryClosure>();
}
