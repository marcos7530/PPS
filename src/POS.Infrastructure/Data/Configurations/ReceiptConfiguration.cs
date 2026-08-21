using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public void Configure(EntityTypeBuilder<Receipt> builder)
    {
        builder.ToTable("receipts", t =>
        {
            t.HasCheckConstraint("ck_receipts_exclusive_parent",
                "(CASE WHEN [transaction_id] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [return_id] IS NULL THEN 0 ELSE 1 END) = 1");
            t.HasCheckConstraint("ck_receipts_payload_json",
                "ISJSON([payload_snapshot]) = 1");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.TransactionId)
            .HasColumnName("transaction_id");

        builder.Property(e => e.ReturnId)
            .HasColumnName("return_id");

        builder.Property(e => e.ReprintCount)
            .HasColumnName("reprint_count")
            .HasDefaultValue(0);

        builder.Property(e => e.FirstEmittedAt)
            .HasColumnName("first_emitted_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.LastChannel)
            .HasColumnName("last_channel")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.PayloadSnapshot)
            .HasColumnName("payload_snapshot")
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.HasOne(e => e.Transaction)
            .WithOne(t => t.Receipt)
            .HasForeignKey<Receipt>(e => e.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Return)
            .WithOne(r => r.Receipt)
            .HasForeignKey<Receipt>(e => e.ReturnId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
