using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", t =>
        {
            t.HasCheckConstraint("ck_products_quantity", "[quantity] >= 0");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.Sku)
            .HasColumnName("sku")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasColumnType("nvarchar(100)")
            .HasMaxLength(100)
            .IsRequired()
            .UseCollation("Latin1_General_100_CI_AI");

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasColumnType("nvarchar(500)")
            .HasMaxLength(500);

        builder.Property(e => e.BarcodeValue)
            .HasColumnName("barcode")
            .HasColumnType("varchar(48)")
            .HasMaxLength(48);

        builder.Property(e => e.BarcodeFormat)
            .HasColumnName("barcode_format")
            .HasColumnType("varchar(10)")
            .HasMaxLength(10);

        builder.Property(e => e.CategoryId)
            .HasColumnName("category_id");

        builder.Property(e => e.SalePrice)
            .HasColumnName("sale_price")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.CostPrice)
            .HasColumnName("cost_price")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.ProfitMarginPercentage)
            .HasColumnName("profit_margin_percentage")
            .HasColumnType("decimal(7,2)");

        builder.Property(e => e.IsPriceManuallyOverridden)
            .HasColumnName("is_price_manually_overridden")
            .HasDefaultValue(false);

        builder.Property(e => e.PriceOverrideBy)
            .HasColumnName("price_override_by");

        builder.Property(e => e.PriceOverrideAt)
            .HasColumnName("price_override_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.Quantity)
            .HasColumnName("quantity");

        builder.Property(e => e.MinStockThreshold)
            .HasColumnName("min_stock_threshold");

        builder.Property(e => e.IsDeactivated)
            .HasColumnName("is_deactivated")
            .HasDefaultValue(false);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion();

        builder.HasIndex(e => e.Sku)
            .IsUnique()
            .HasDatabaseName("ux_products_sku");

        builder.HasIndex(e => e.BarcodeValue)
            .IsUnique()
            .HasDatabaseName("ux_products_barcode")
            .HasFilter("[barcode] IS NOT NULL");

        builder.HasOne(e => e.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.PriceOverrideByUser)
            .WithMany()
            .HasForeignKey(e => e.PriceOverrideBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(e => e.IsLowStock);
    }
}
