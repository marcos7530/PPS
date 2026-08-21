using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs", t =>
        {
            t.HasCheckConstraint("ck_audit_logs_related_entity_ids_json",
                "[related_entity_ids] IS NULL OR ISJSON([related_entity_ids]) = 1");
            t.HasCheckConstraint("ck_audit_logs_before_state_json",
                "[before_state] IS NULL OR ISJSON([before_state]) = 1");
            t.HasCheckConstraint("ck_audit_logs_after_state_json",
                "[after_state] IS NULL OR ISJSON([after_state]) = 1");
            t.HasCheckConstraint("ck_audit_logs_metadata_json",
                "[metadata] IS NULL OR ISJSON([metadata]) = 1");
        });

        builder.HasKey(e => e.Id)
            .IsClustered(false);

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("datetime2(3)");

        builder.Property(e => e.UserId)
            .HasColumnName("user_id");

        builder.Property(e => e.UsernameSnapshot)
            .HasColumnName("username_snapshot")
            .HasColumnType("varchar(50)")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.OperationType)
            .HasColumnName("operation_type")
            .HasColumnType("varchar(40)")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(e => e.EntityType)
            .HasColumnName("entity_type")
            .HasColumnType("varchar(40)")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(e => e.EntityId)
            .HasColumnName("entity_id");

        builder.Property(e => e.RelatedEntityIds)
            .HasColumnName("related_entity_ids")
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.Outcome)
            .HasColumnName("outcome")
            .HasColumnType("varchar(10)")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(e => e.ErrorCode)
            .HasColumnName("error_code")
            .HasColumnType("varchar(60)")
            .HasMaxLength(60);

        builder.Property(e => e.BeforeState)
            .HasColumnName("before_state")
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.AfterState)
            .HasColumnName("after_state")
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("nvarchar(max)");

        builder.Property(e => e.SessionId)
            .HasColumnName("session_id");

        builder.Property(e => e.IpAddress)
            .HasColumnName("ip_address")
            .HasColumnType("varchar(45)")
            .HasMaxLength(45);

        // Clustered index on occurred_at for range queries
        builder.HasIndex(e => new { e.OccurredAt, e.Id })
            .IsClustered()
            .HasDatabaseName("ix_audit_logs_occurred");

        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
