using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class ReturnLineItemConfiguration : IEntityTypeConfiguration<ReturnLineItem>
{
    public void Configure(EntityTypeBuilder<ReturnLineItem> builder)
    {
        builder.ToTable("return_line_items");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.ReturnId)
            .HasColumnName("return_id");

        builder.Property(e => e.LineItemId)
            .HasColumnName("line_item_id");

        builder.Property(e => e.ProductId)
            .HasColumnName("product_id");

        builder.Property(e => e.ReturnQuantity)
            .HasColumnName("return_quantity");

        builder.Property(e => e.UnitPrice)
            .HasColumnName("unit_price")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.LineRefundAmount)
            .HasColumnName("line_refund_amount")
            .HasColumnType("decimal(9,2)");

        builder.HasIndex(e => new { e.ReturnId, e.LineItemId })
            .IsUnique()
            .HasDatabaseName("ux_return_line_items_return_line");

        builder.HasOne(e => e.Return)
            .WithMany(r => r.LineItems)
            .HasForeignKey(e => e.ReturnId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.OriginalLineItem)
            .WithMany(li => li.ReturnLineItems)
            .HasForeignKey(e => e.LineItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
