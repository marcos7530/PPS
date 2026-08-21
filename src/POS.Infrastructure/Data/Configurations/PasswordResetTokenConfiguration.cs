using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");

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

        builder.Property(e => e.ConsumedAt)
            .HasColumnName("consumed_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.InvalidatedAt)
            .HasColumnName("invalidated_at")
            .HasColumnType("datetime2(3)");

        builder.HasIndex(e => e.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_password_reset_tokens_token_hash");

        // Filtered unique index: one active token per user
        builder.HasIndex(e => e.UserId)
            .IsUnique()
            .HasDatabaseName("ux_password_reset_tokens_active")
            .HasFilter("[consumed_at] IS NULL AND [invalidated_at] IS NULL");

        builder.HasOne(e => e.User)
            .WithMany(u => u.PasswordResetTokens)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
