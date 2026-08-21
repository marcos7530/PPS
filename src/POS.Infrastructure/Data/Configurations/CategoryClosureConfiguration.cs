using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class CategoryClosureConfiguration : IEntityTypeConfiguration<CategoryClosure>
{
    public void Configure(EntityTypeBuilder<CategoryClosure> builder)
    {
        builder.ToTable("category_closures");

        builder.HasKey(e => new { e.AncestorId, e.DescendantId });

        builder.Property(e => e.AncestorId)
            .HasColumnName("ancestor_id");

        builder.Property(e => e.DescendantId)
            .HasColumnName("descendant_id");

        builder.Property(e => e.Depth)
            .HasColumnName("depth");

        builder.HasOne(e => e.Ancestor)
            .WithMany(c => c.AncestorClosures)
            .HasForeignKey(e => e.AncestorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Descendant)
            .WithMany(c => c.DescendantClosures)
            .HasForeignKey(e => e.DescendantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
