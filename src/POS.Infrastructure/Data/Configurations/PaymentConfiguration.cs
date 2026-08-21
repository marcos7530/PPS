using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.TransactionId)
            .HasColumnName("transaction_id");

        builder.Property(e => e.Method)
            .HasColumnName("method")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Amount)
            .HasColumnName("amount")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.VoucherId)
            .HasColumnName("voucher_id");

        builder.Property(e => e.StoreCreditCustomerId)
            .HasColumnName("store_credit_customer_id");

        builder.Property(e => e.IsConsumptionActive)
            .HasColumnName("is_consumption_active")
            .HasDefaultValue(true);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)");

        // Filtered unique index: active voucher payment (prevents double-spend)
        builder.HasIndex(e => e.VoucherId)
            .IsUnique()
            .HasDatabaseName("ux_payments_voucher_active")
            .HasFilter("[voucher_id] IS NOT NULL AND [is_consumption_active] = 1");

        builder.HasOne(e => e.Transaction)
            .WithMany(t => t.Payments)
            .HasForeignKey(e => e.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Voucher)
            .WithMany()
            .HasForeignKey(e => e.VoucherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.StoreCreditCustomer)
            .WithMany()
            .HasForeignKey(e => e.StoreCreditCustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
