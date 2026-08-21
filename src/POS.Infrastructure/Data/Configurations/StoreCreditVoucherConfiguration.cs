using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class StoreCreditVoucherConfiguration : IEntityTypeConfiguration<StoreCreditVoucher>
{
    public void Configure(EntityTypeBuilder<StoreCreditVoucher> builder)
    {
        builder.ToTable("store_credit_vouchers");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.Code)
            .HasColumnName("code")
            .HasColumnType("varchar(32)")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.Amount)
            .HasColumnName("amount")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.IssuedAt)
            .HasColumnName("issued_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(10)")
            .HasMaxLength(10)
            .IsRequired()
            .HasDefaultValue("unused");

        builder.Property(e => e.UsedAt)
            .HasColumnName("used_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.UsedInTransactionId)
            .HasColumnName("used_in_transaction_id");

        builder.Property(e => e.OriginReturnId)
            .HasColumnName("origin_return_id");

        builder.HasIndex(e => e.Code)
            .IsUnique()
            .HasDatabaseName("ux_store_credit_vouchers_code");

        builder.HasOne(e => e.UsedInTransaction)
            .WithMany()
            .HasForeignKey(e => e.UsedInTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.OriginReturn)
            .WithMany()
            .HasForeignKey(e => e.OriginReturnId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
