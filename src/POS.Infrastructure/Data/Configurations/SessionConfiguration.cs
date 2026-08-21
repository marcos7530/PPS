using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.UserId)
            .HasColumnName("user_id");

        builder.Property(e => e.TokenHash)
            .HasColumnName("token_hash")
            .HasColumnType("varbinary(32)")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.RevokedAt)
            .HasColumnName("revoked_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.IpAddress)
            .HasColumnName("ip_address")
            .HasColumnType("varchar(45)")
            .HasMaxLength(45);

        builder.Property(e => e.UserAgent)
            .HasColumnName("user_agent")
            .HasColumnType("varchar(400)")
            .HasMaxLength(400);

        builder.HasIndex(e => e.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_sessions_token_hash");

        builder.HasOne(e => e.User)
            .WithMany(u => u.Sessions)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
