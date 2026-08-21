using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("shifts", t =>
        {
            t.HasCheckConstraint("ck_shifts_variance_notes",
                "[status] = 'open' OR ABS([variance_amount]) <= 10.00 OR LEN([variance_notes]) BETWEEN 1 AND 500");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.CashDrawerId)
            .HasColumnName("cash_drawer_id")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.UserId)
            .HasColumnName("user_id");

        builder.Property(e => e.OpenedAt)
            .HasColumnName("opened_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.OperatingDay)
            .HasColumnName("operating_day")
            .HasColumnType("date");

        builder.Property(e => e.OpeningCashAmount)
            .HasColumnName("opening_cash_amount")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(10)")
            .HasMaxLength(10)
            .IsRequired()
            .HasDefaultValue("open");

        builder.Property(e => e.ClosedAt)
            .HasColumnName("closed_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.ClosingCashAmount)
            .HasColumnName("closing_cash_amount")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.ExpectedCashBalance)
            .HasColumnName("expected_cash_balance")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.VarianceAmount)
            .HasColumnName("variance_amount")
            .HasColumnType("decimal(9,2)");

        builder.Property(e => e.VarianceStatus)
            .HasColumnName("variance_status")
            .HasColumnType("varchar(10)")
            .HasMaxLength(10);

        builder.Property(e => e.VarianceNotes)
            .HasColumnName("variance_notes")
            .HasColumnType("nvarchar(500)")
            .HasMaxLength(500);

        // Filtered index: one active (open) shift per cash drawer
        builder.HasIndex(e => e.CashDrawerId)
            .IsUnique()
            .HasDatabaseName("ux_shifts_open_drawer")
            .HasFilter("[status] = 'open'");

        // Filtered index: one active (open) shift per user
        builder.HasIndex(e => e.UserId)
            .IsUnique()
            .HasDatabaseName("ux_shifts_open_user")
            .HasFilter("[status] = 'open'");

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
