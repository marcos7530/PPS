using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class ReturnConfiguration : IEntityTypeConfiguration<Return>
{
    public void Configure(EntityTypeBuilder<Return> builder)
    {
        builder.ToTable("returns");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.OriginalTransactionId)
            .HasColumnName("original_transaction_id");

        builder.Property(e => e.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.OperatingDay)
            .HasColumnName("operating_day")
            .HasColumnType("date");

        builder.Property(e => e.UserId)
            .HasColumnName("user_id");

        builder.Property(e => e.ShiftId)
            .HasColumnName("shift_id");

        builder.Property(e => e.RefundAmount)
            .HasColumnName("refund_amount")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.RefundMethod)
            .HasColumnName("refund_method")
            .HasColumnType("varchar(25)")
            .HasMaxLength(25)
            .IsRequired();

        builder.Property(e => e.ReasonCode)
            .HasColumnName("reason_code")
            .HasColumnType("varchar(25)")
            .HasMaxLength(25)
            .IsRequired();

        builder.Property(e => e.AuthorizedBy)
            .HasColumnName("authorized_by");

        builder.HasOne(e => e.OriginalTransaction)
            .WithMany(t => t.Returns)
            .HasForeignKey(e => e.OriginalTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Shift)
            .WithMany(s => s.Returns)
            .HasForeignKey(e => e.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.AuthorizedByUser)
            .WithMany()
            .HasForeignKey(e => e.AuthorizedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
