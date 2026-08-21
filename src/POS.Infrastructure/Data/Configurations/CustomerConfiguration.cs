using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasColumnType("nvarchar(100)")
            .HasMaxLength(100)
            .IsRequired()
            .UseCollation("Latin1_General_100_CI_AI");

        builder.Property(e => e.Email)
            .HasColumnName("email")
            .HasColumnType("nvarchar(100)")
            .HasMaxLength(100)
            .UseCollation("Latin1_General_100_CI_AS");

        builder.Property(e => e.Phone)
            .HasColumnName("phone")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20);

        builder.Property(e => e.PhoneNormalized)
            .HasColumnName("phone_normalized")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20);

        builder.Property(e => e.Notes)
            .HasColumnName("notes")
            .HasColumnType("nvarchar(500)")
            .HasMaxLength(500);

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by");

        builder.HasIndex(e => e.Email)
            .IsUnique()
            .HasDatabaseName("ux_customers_email")
            .HasFilter("[email] IS NOT NULL");

        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
