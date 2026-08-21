using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class DashboardConfigurationConfiguration : IEntityTypeConfiguration<DashboardConfiguration>
{
    public void Configure(EntityTypeBuilder<DashboardConfiguration> builder)
    {
        builder.ToTable("dashboard_configurations", t =>
        {
            t.HasCheckConstraint("ck_dashboard_configurations_widgets_json", "ISJSON([widgets]) = 1");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.UserId)
            .HasColumnName("user_id");

        builder.Property(e => e.Widgets)
            .HasColumnName("widgets")
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime2(3)");

        builder.HasIndex(e => e.UserId)
            .IsUnique()
            .HasDatabaseName("ux_dashboard_configurations_user_id");

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
