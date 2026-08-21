namespace POS.Domain.Entities;

/// <summary>
/// Represents a system role (Administrator, Manager, Cashier, Viewer).
/// </summary>
public class Role
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(20)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    // Seed data identifiers
    public static class WellKnown
    {
        public static readonly Guid AdministratorId = new("10000000-0000-0000-0000-000000000001");
        public static readonly Guid ManagerId = new("10000000-0000-0000-0000-000000000002");
        public static readonly Guid CashierId = new("10000000-0000-0000-0000-000000000003");
        public static readonly Guid ViewerId = new("10000000-0000-0000-0000-000000000004");
    }
}
