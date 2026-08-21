using Microsoft.EntityFrameworkCore.Migrations;

namespace POS.Infrastructure.Data.Migrations;

/// <summary>
/// Initial migration that applies audit immutability DDL:
/// - Partition function/scheme for monthly range on occurred_at
/// - pos_app_role with SELECT/INSERT only on audit_logs
/// - DENY UPDATE, DELETE, ALTER, CONTROL on audit_logs
/// - INSTEAD OF UPDATE/DELETE triggers (THROW 50001)
/// - READ_COMMITTED_SNAPSHOT ON
/// - Conditional LEDGER (APPEND_ONLY) for SQL Server 2022+
/// Requirements: 1.3, 1.4
/// </summary>
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ─────────────────────────────────────────────────────────────────
        // 1. Partition function and scheme for audit_logs
        //    Monthly partitioning on occurred_at covering 2025–2032
        // ─────────────────────────────────────────────────────────────────

        migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.partition_functions WHERE name = 'pf_audit_monthly')
BEGIN
    CREATE PARTITION FUNCTION pf_audit_monthly (datetime2(3))
    AS RANGE RIGHT FOR VALUES (
        '2025-01-01','2025-02-01','2025-03-01','2025-04-01','2025-05-01','2025-06-01',
        '2025-07-01','2025-08-01','2025-09-01','2025-10-01','2025-11-01','2025-12-01',
        '2026-01-01','2026-02-01','2026-03-01','2026-04-01','2026-05-01','2026-06-01',
        '2026-07-01','2026-08-01','2026-09-01','2026-10-01','2026-11-01','2026-12-01',
        '2027-01-01','2027-02-01','2027-03-01','2027-04-01','2027-05-01','2027-06-01',
        '2027-07-01','2027-08-01','2027-09-01','2027-10-01','2027-11-01','2027-12-01',
        '2028-01-01','2028-02-01','2028-03-01','2028-04-01','2028-05-01','2028-06-01',
        '2028-07-01','2028-08-01','2028-09-01','2028-10-01','2028-11-01','2028-12-01',
        '2029-01-01','2029-02-01','2029-03-01','2029-04-01','2029-05-01','2029-06-01',
        '2029-07-01','2029-08-01','2029-09-01','2029-10-01','2029-11-01','2029-12-01',
        '2030-01-01','2030-02-01','2030-03-01','2030-04-01','2030-05-01','2030-06-01',
        '2030-07-01','2030-08-01','2030-09-01','2030-10-01','2030-11-01','2030-12-01',
        '2031-01-01','2031-02-01','2031-03-01','2031-04-01','2031-05-01','2031-06-01',
        '2031-07-01','2031-08-01','2031-09-01','2031-10-01','2031-11-01','2031-12-01',
        '2032-01-01','2032-02-01','2032-03-01','2032-04-01','2032-05-01','2032-06-01',
        '2032-07-01','2032-08-01','2032-09-01','2032-10-01','2032-11-01','2032-12-01'
    );
END;
");

        migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.partition_schemes WHERE name = 'ps_audit_monthly')
BEGIN
    CREATE PARTITION SCHEME ps_audit_monthly
    AS PARTITION pf_audit_monthly ALL TO ([PRIMARY]);
END;
");

        // ─────────────────────────────────────────────────────────────────
        // 2. Security: pos_app_role with append-only permissions
        // ─────────────────────────────────────────────────────────────────

        migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'pos_app_role' AND type = 'R')
    CREATE ROLE pos_app_role;
");

        migrationBuilder.Sql("GRANT SELECT, INSERT ON dbo.audit_logs TO pos_app_role;");

        migrationBuilder.Sql("DENY UPDATE, DELETE, ALTER, CONTROL ON dbo.audit_logs TO pos_app_role;");

        // ─────────────────────────────────────────────────────────────────
        // 3. Triggers: prevent modification and deletion of audit entries
        // ─────────────────────────────────────────────────────────────────

        migrationBuilder.Sql(@"
CREATE OR ALTER TRIGGER tr_audit_logs_no_update ON dbo.audit_logs
INSTEAD OF UPDATE
AS
BEGIN
    THROW 50001, 'Audit log entries cannot be modified', 1;
END;
");

        migrationBuilder.Sql(@"
CREATE OR ALTER TRIGGER tr_audit_logs_no_delete ON dbo.audit_logs
INSTEAD OF DELETE
AS
BEGIN
    THROW 50001, 'Audit log entries cannot be deleted', 1;
END;
");

        // ─────────────────────────────────────────────────────────────────
        // 4. READ_COMMITTED_SNAPSHOT for reduced lock contention
        // ─────────────────────────────────────────────────────────────────

        migrationBuilder.Sql("ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON;");

        // ─────────────────────────────────────────────────────────────────
        // 5. Conditional Ledger for SQL Server 2022+ (append-only)
        //    Provides cryptographic verification of audit immutability
        // ─────────────────────────────────────────────────────────────────

        migrationBuilder.Sql(@"
IF CAST(SERVERPROPERTY('ProductMajorVersion') AS INT) >= 16
BEGIN
    EXEC sp_executesql N'ALTER TABLE dbo.audit_logs SET (LEDGER = ON (APPEND_ONLY = ON))';
END;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Note: Reversing audit immutability should be done with extreme caution.
        // In production this migration should never be reverted.

        migrationBuilder.Sql(@"
IF CAST(SERVERPROPERTY('ProductMajorVersion') AS INT) >= 16
BEGIN
    EXEC sp_executesql N'ALTER TABLE dbo.audit_logs SET (LEDGER = OFF)';
END;
");

        migrationBuilder.Sql("ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT OFF;");

        migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.tr_audit_logs_no_delete;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.tr_audit_logs_no_update;");

        migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'pos_app_role' AND type = 'R')
BEGIN
    REVOKE SELECT, INSERT ON dbo.audit_logs FROM pos_app_role;
    REVOKE UPDATE, DELETE, ALTER, CONTROL ON dbo.audit_logs FROM pos_app_role;
    DROP ROLE pos_app_role;
END;
");

        migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.partition_schemes WHERE name = 'ps_audit_monthly')
    DROP PARTITION SCHEME ps_audit_monthly;
");

        migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.partition_functions WHERE name = 'pf_audit_monthly')
    DROP PARTITION FUNCTION pf_audit_monthly;
");
    }
}
