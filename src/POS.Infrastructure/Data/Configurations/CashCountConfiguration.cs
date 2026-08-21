using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class CashCountConfiguration : IEntityTypeConfiguration<CashCount>
{
    public void Configure(EntityTypeBuilder<CashCount> builder)
    {
        builder.ToTable("cash_counts", t =>
        {
            t.HasCheckConstraint("ck_cash_counts_breakdown_json", "ISJSON([breakdown]) = 1");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.ShiftId)
            .HasColumnName("shift_id");

        builder.Property(e => e.CountType)
            .HasColumnName("count_type")
            .HasColumnType("varchar(10)")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.TotalAmount)
            .HasColumnName("total_amount")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.Breakdown)
            .HasColumnName("breakdown")
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(e => e.CountedAt)
            .HasColumnName("counted_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.CountedBy)
            .HasColumnName("counted_by");

        builder.HasIndex(e => new { e.ShiftId, e.CountType })
            .IsUnique()
            .HasDatabaseName("ux_cash_counts_shift_type");

        builder.HasOne(e => e.Shift)
            .WithMany(s => s.CashCounts)
            .HasForeignKey(e => e.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CountedByUser)
            .WithMany()
            .HasForeignKey(e => e.CountedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
