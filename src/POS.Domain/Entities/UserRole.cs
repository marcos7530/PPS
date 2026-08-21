namespace POS.Domain.Entities;

/// <summary>
/// Many-to-many relationship between Users and Roles. Composite PK: (UserId, RoleId).
/// </summary>
public class UserRole
{
    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }

    public DateTimeOffset AssignedAt { get; set; }

    public Guid AssignedBy { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(RoleId))]
    public Role Role { get; set; } = null!;

    [ForeignKey(nameof(AssignedBy))]
    public User AssignedByUser { get; set; } = null!;
}
