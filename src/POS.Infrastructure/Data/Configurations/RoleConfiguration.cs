using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasColumnType("nvarchar(200)")
            .HasMaxLength(200);

        builder.HasIndex(e => e.Name)
            .IsUnique()
            .HasDatabaseName("ux_roles_name");

        // Seed data
        builder.HasData(
            new Role { Id = Role.WellKnown.AdministratorId, Name = "Administrator", Description = "Full system access" },
            new Role { Id = Role.WellKnown.ManagerId, Name = "Manager", Description = "Store management access" },
            new Role { Id = Role.WellKnown.CashierId, Name = "Cashier", Description = "Point of sale operations" },
            new Role { Id = Role.WellKnown.ViewerId, Name = "Viewer", Description = "Read-only access" }
        );
    }
}
