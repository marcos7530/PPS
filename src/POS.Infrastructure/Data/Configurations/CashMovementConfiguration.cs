using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> builder)
    {
        builder.ToTable("cash_movements");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.ShiftId)
            .HasColumnName("shift_id");

        builder.Property(e => e.MovementType)
            .HasColumnName("movement_type")
            .HasColumnType("varchar(15)")
            .HasMaxLength(15)
            .IsRequired();

        builder.Property(e => e.Amount)
            .HasColumnName("amount")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.Reason)
            .HasColumnName("reason")
            .HasColumnType("nvarchar(200)")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Notes)
            .HasColumnName("notes")
            .HasColumnType("nvarchar(200)")
            .HasMaxLength(200);

        builder.Property(e => e.UserId)
            .HasColumnName("user_id");

        builder.Property(e => e.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("datetime2(3)");

        builder.HasOne(e => e.Shift)
            .WithMany(s => s.CashMovements)
            .HasForeignKey(e => e.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
