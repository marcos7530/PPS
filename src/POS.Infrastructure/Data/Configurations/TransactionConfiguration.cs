using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions", t =>
        {
            t.HasCheckConstraint("ck_transactions_final_amount",
                "[final_amount] = [subtotal] + [tax_amount] - [discount_amount]");

            t.HasCheckConstraint("ck_transactions_void_consistency",
                "[is_voided] = 0 OR ([voided_at] IS NOT NULL AND [voided_by] IS NOT NULL AND [void_reason] IS NOT NULL AND LEN([void_notes]) BETWEEN 1 AND 500)");
        });

        builder.HasKey(e => e.Id)
            .IsClustered(false);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.TransactionNumber)
            .HasColumnName("transaction_number");

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

        builder.Property(e => e.CustomerId)
            .HasColumnName("customer_id");

        builder.Property(e => e.Subtotal)
            .HasColumnName("subtotal")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.TaxAmount)
            .HasColumnName("tax_amount")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.DiscountAmount)
            .HasColumnName("discount_amount")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.FinalAmount)
            .HasColumnName("final_amount")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.AmountReceived)
            .HasColumnName("amount_received")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.ChangeDue)
            .HasColumnName("change_due")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.TaxRateApplied)
            .HasColumnName("tax_rate_applied")
            .HasColumnType("decimal(5,2)");

        builder.Property(e => e.DiscountAuthorizedBy)
            .HasColumnName("discount_authorized_by");

        builder.Property(e => e.IsVoided)
            .HasColumnName("is_voided")
            .HasDefaultValue(false);

        builder.Property(e => e.VoidedAt)
            .HasColumnName("voided_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.VoidedBy)
            .HasColumnName("voided_by");

        builder.Property(e => e.VoidReason)
            .HasColumnName("void_reason")
            .HasColumnType("varchar(30)")
            .HasMaxLength(30);

        builder.Property(e => e.VoidNotes)
            .HasColumnName("void_notes")
            .HasColumnType("nvarchar(500)")
            .HasMaxLength(500);

        // Clustered index on range columns for efficient queries
        builder.HasIndex(e => new { e.OperatingDay, e.CompletedAt })
            .IsClustered()
            .HasDatabaseName("ix_transactions_operating_day_completed");

        builder.HasIndex(e => e.TransactionNumber)
            .IsUnique()
            .HasDatabaseName("ux_transactions_number");

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Shift)
            .WithMany(s => s.Transactions)
            .HasForeignKey(e => e.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Customer)
            .WithMany(c => c.Transactions)
            .HasForeignKey(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.DiscountAuthorizedByUser)
            .WithMany()
            .HasForeignKey(e => e.DiscountAuthorizedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.VoidedByUser)
            .WithMany()
            .HasForeignKey(e => e.VoidedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
