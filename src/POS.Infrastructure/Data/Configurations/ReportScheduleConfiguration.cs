using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class ReportScheduleConfiguration : IEntityTypeConfiguration<ReportSchedule>
{
    public void Configure(EntityTypeBuilder<ReportSchedule> builder)
    {
        builder.ToTable("report_schedules", t =>
        {
            t.HasCheckConstraint("ck_report_schedules_recipients_json", "ISJSON([recipients]) = 1");
            t.HasCheckConstraint("ck_report_schedules_filter_json", "ISJSON([filter_json]) = 1");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(e => e.ReportType)
            .HasColumnName("report_type")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Frequency)
            .HasColumnName("frequency")
            .HasColumnType("varchar(10)")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.ExportFormat)
            .HasColumnName("export_format")
            .HasColumnType("varchar(10)")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.Recipients)
            .HasColumnName("recipients")
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(e => e.FilterJson)
            .HasColumnName("filter_json")
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(e => e.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(e => e.LastRunAt)
            .HasColumnName("last_run_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.LastRunStatus)
            .HasColumnName("last_run_status")
            .HasColumnType("varchar(10)")
            .HasMaxLength(10);

        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
