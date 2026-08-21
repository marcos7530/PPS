namespace POS.Domain.Entities;

/// <summary>
/// Closure table for efficient hierarchical category queries.
/// Composite PK: (AncestorId, DescendantId). Depth 0 = self-reference, max 4.
/// </summary>
public class CategoryClosure
{
    public Guid AncestorId { get; set; }

    public Guid DescendantId { get; set; }

    /// <summary>
    /// Distance between ancestor and descendant (0 = self, max 4).
    /// </summary>
    [Range(0, 4)]
    public short Depth { get; set; }

    // Navigation properties
    [ForeignKey(nameof(AncestorId))]
    public Category Ancestor { get; set; } = null!;

    [ForeignKey(nameof(DescendantId))]
    public Category Descendant { get; set; } = null!;
}
