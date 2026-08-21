using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("product_images");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.ProductId)
            .HasColumnName("product_id");

        builder.Property(e => e.OriginalFileName)
            .HasColumnName("original_file_name")
            .HasColumnType("nvarchar(255)")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.ContentType)
            .HasColumnName("content_type")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.ByteSize)
            .HasColumnName("byte_size");

        builder.Property(e => e.WidthPx)
            .HasColumnName("width_px");

        builder.Property(e => e.HeightPx)
            .HasColumnName("height_px");

        builder.Property(e => e.StoragePath)
            .HasColumnName("storage_path")
            .HasColumnType("nvarchar(400)")
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(e => e.ThumbnailPath)
            .HasColumnName("thumbnail_path")
            .HasColumnType("nvarchar(400)")
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(e => e.UploadedBy)
            .HasColumnName("uploaded_by");

        builder.Property(e => e.UploadedAt)
            .HasColumnName("uploaded_at")
            .HasColumnType("datetime2(3)");

        builder.HasIndex(e => e.ProductId)
            .IsUnique()
            .HasDatabaseName("ux_product_images_product_id");

        builder.HasOne(e => e.Product)
            .WithOne(p => p.Image)
            .HasForeignKey<ProductImage>(e => e.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.UploadedByUser)
            .WithMany()
            .HasForeignKey(e => e.UploadedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
