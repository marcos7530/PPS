using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class StoreCreditConfiguration : IEntityTypeConfiguration<StoreCredit>
{
    public void Configure(EntityTypeBuilder<StoreCredit> builder)
    {
        builder.ToTable("store_credits");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.CustomerId)
            .HasColumnName("customer_id");

        builder.Property(e => e.Balance)
            .HasColumnName("balance")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime2(3)");

        builder.HasIndex(e => e.CustomerId)
            .IsUnique()
            .HasDatabaseName("ux_store_credits_customer_id");

        builder.HasOne(e => e.Customer)
            .WithOne(c => c.StoreCredit)
            .HasForeignKey<StoreCredit>(e => e.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
