using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasColumnType("nvarchar(100)")
            .HasMaxLength(100)
            .IsRequired()
            .UseCollation("Latin1_General_100_CI_AS");

        builder.Property(e => e.ParentCategoryId)
            .HasColumnName("parent_category_id");

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasColumnType("nvarchar(500)")
            .HasMaxLength(500);

        builder.Property(e => e.DisplayOrder)
            .HasColumnName("display_order")
            .HasDefaultValue(1);

        builder.Property(e => e.ProfitMarginPercentage)
            .HasColumnName("profit_margin_percentage")
            .HasColumnType("decimal(7,2)");

        builder.Property(e => e.Depth)
            .HasColumnName("depth")
            .HasDefaultValue((short)1);

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime2(3)");

        // Unique index on (ParentCategoryId, Name) — covers root categories too
        builder.HasIndex(e => new { e.ParentCategoryId, e.Name })
            .IsUnique()
            .HasDatabaseName("ux_categories_parent_name");

        builder.HasOne(e => e.ParentCategory)
            .WithMany(c => c.ChildCategories)
            .HasForeignKey(e => e.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
