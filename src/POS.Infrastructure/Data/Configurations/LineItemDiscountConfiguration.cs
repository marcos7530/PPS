using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class LineItemDiscountConfiguration : IEntityTypeConfiguration<LineItemDiscount>
{
    public void Configure(EntityTypeBuilder<LineItemDiscount> builder)
    {
        builder.ToTable("line_item_discounts");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.LineItemId)
            .HasColumnName("line_item_id");

        builder.Property(e => e.DiscountType)
            .HasColumnName("discount_type")
            .HasColumnType("varchar(15)")
            .HasMaxLength(15)
            .IsRequired();

        builder.Property(e => e.Percentage)
            .HasColumnName("percentage")
            .HasColumnType("decimal(5,2)");

        builder.Property(e => e.Amount)
            .HasColumnName("amount")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.Reason)
            .HasColumnName("reason")
            .HasColumnType("varchar(30)")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.Notes)
            .HasColumnName("notes")
            .HasColumnType("nvarchar(200)")
            .HasMaxLength(200);

        builder.Property(e => e.AppliedBy)
            .HasColumnName("applied_by");

        builder.Property(e => e.AuthorizedBy)
            .HasColumnName("authorized_by");

        builder.HasIndex(e => e.LineItemId)
            .IsUnique()
            .HasDatabaseName("ux_line_item_discounts_line_item_id");

        builder.HasOne(e => e.LineItem)
            .WithOne(li => li.Discount)
            .HasForeignKey<LineItemDiscount>(e => e.LineItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.AppliedByUser)
            .WithMany()
            .HasForeignKey(e => e.AppliedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.AuthorizedByUser)
            .WithMany()
            .HasForeignKey(e => e.AuthorizedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
