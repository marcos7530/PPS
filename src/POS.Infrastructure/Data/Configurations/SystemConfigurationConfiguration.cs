using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class SystemConfigurationConfiguration : IEntityTypeConfiguration<SystemConfiguration>
{
    public void Configure(EntityTypeBuilder<SystemConfiguration> builder)
    {
        builder.ToTable("system_configurations", t =>
        {
            t.HasCheckConstraint("ck_system_configurations_single_row", "[id] = 1");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValue((short)1);

        builder.Property(e => e.BusinessName)
            .HasColumnName("business_name")
            .HasColumnType("nvarchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.BusinessAddress)
            .HasColumnName("business_address")
            .HasColumnType("nvarchar(500)")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.TaxRatePercentage)
            .HasColumnName("tax_rate_percentage")
            .HasColumnType("decimal(5,2)");

        builder.Property(e => e.CurrencyCode)
            .HasColumnName("currency_code")
            .HasColumnType("varchar(3)")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(e => e.BusinessTimeZone)
            .HasColumnName("business_time_zone")
            .HasColumnType("varchar(60)")
            .HasMaxLength(60)
            .IsRequired()
            .HasDefaultValue("America/Argentina/Buenos_Aires");

        builder.Property(e => e.GlobalProfitMarginPercentage)
            .HasColumnName("global_profit_margin_percentage")
            .HasColumnType("decimal(7,2)")
            .HasDefaultValue(30m);

        builder.Property(e => e.CashierDiscountLimitPercentage)
            .HasColumnName("cashier_discount_limit_percentage")
            .HasColumnType("decimal(5,2)")
            .HasDefaultValue(10m);

        builder.Property(e => e.ReceiptFooterText)
            .HasColumnName("receipt_footer_text")
            .HasColumnType("nvarchar(200)")
            .HasMaxLength(200);

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.UpdatedBy)
            .HasColumnName("updated_by");

        builder.HasOne(e => e.UpdatedByUser)
            .WithMany()
            .HasForeignKey(e => e.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
