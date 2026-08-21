using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.Username)
            .HasColumnName("username")
            .HasColumnType("nvarchar(50)")
            .HasMaxLength(50)
            .IsRequired()
            .UseCollation("Latin1_General_100_CI_AS");

        builder.Property(e => e.Email)
            .HasColumnName("email")
            .HasColumnType("nvarchar(100)")
            .HasMaxLength(100)
            .IsRequired()
            .UseCollation("Latin1_General_100_CI_AS");

        builder.Property(e => e.PasswordHash)
            .HasColumnName("password_hash")
            .HasColumnType("varchar(72)")
            .IsRequired();

        builder.Property(e => e.FullName)
            .HasColumnName("full_name")
            .HasColumnType("nvarchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(e => e.FailedLoginCount)
            .HasColumnName("failed_login_count")
            .HasDefaultValue((short)0);

        builder.Property(e => e.FailedWindowStartedAt)
            .HasColumnName("failed_window_started_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.LockedUntil)
            .HasColumnName("locked_until")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion();

        builder.HasIndex(e => e.Username)
            .IsUnique()
            .HasDatabaseName("ux_users_username");

        builder.HasIndex(e => e.Email)
            .IsUnique()
            .HasDatabaseName("ux_users_email");
    }
}
