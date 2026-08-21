using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class TransactionLineItemConfiguration : IEntityTypeConfiguration<TransactionLineItem>
{
    public void Configure(EntityTypeBuilder<TransactionLineItem> builder)
    {
        builder.ToTable("transaction_line_items");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.TransactionId)
            .HasColumnName("transaction_id");

        builder.Property(e => e.ProductId)
            .HasColumnName("product_id");

        builder.Property(e => e.ProductNameSnapshot)
            .HasColumnName("product_name_snapshot")
            .HasColumnType("nvarchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Quantity)
            .HasColumnName("quantity");

        builder.Property(e => e.UnitPrice)
            .HasColumnName("unit_price")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.RecordedCostPrice)
            .HasColumnName("recorded_cost_price")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.LineDiscountAmount)
            .HasColumnName("line_discount_amount")
            .HasColumnType("decimal(9,2)")
            .HasDefaultValue(0m);

        builder.Property(e => e.LineAmount)
            .HasColumnName("line_amount")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.ReturnedQuantity)
            .HasColumnName("returned_quantity")
            .HasDefaultValue(0);

        builder.HasIndex(e => new { e.TransactionId, e.ProductId })
            .IsUnique()
            .HasDatabaseName("ux_transaction_line_items_tx_product");

        builder.HasOne(e => e.Transaction)
            .WithMany(t => t.LineItems)
            .HasForeignKey(e => e.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
