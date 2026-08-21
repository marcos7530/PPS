using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class DailySalesAggregateConfiguration : IEntityTypeConfiguration<DailySalesAggregate>
{
    public void Configure(EntityTypeBuilder<DailySalesAggregate> builder)
    {
        builder.ToTable("daily_sales_aggregates");

        builder.HasKey(e => new { e.OperatingDay, e.CategoryId, e.ProductId });

        builder.Property(e => e.OperatingDay)
            .HasColumnName("operating_day")
            .HasColumnType("date");

        builder.Property(e => e.CategoryId)
            .HasColumnName("category_id");

        builder.Property(e => e.ProductId)
            .HasColumnName("product_id");

        builder.Property(e => e.NetSalesAmount)
            .HasColumnName("net_sales_amount")
            .HasColumnType("decimal(12,2)");

        builder.Property(e => e.TransactionCount)
            .HasColumnName("transaction_count");

        builder.Property(e => e.QuantitySold)
            .HasColumnName("quantity_sold");

        builder.Property(e => e.GrossMarginAmount)
            .HasColumnName("gross_margin_amount")
            .HasColumnType("decimal(12,2)");

        builder.Property(e => e.RefreshedAt)
            .HasColumnName("refreshed_at")
            .HasColumnType("datetime2(3)");

        builder.HasOne(e => e.Category)
            .WithMany()
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
