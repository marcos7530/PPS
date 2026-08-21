-- =============================================================================
-- AuditImmutability.sql
-- Ensures audit_logs table is append-only via DDL constraints, triggers,
-- role-based permissions, partitioning, and optional SQL Server 2022 Ledger.
-- Requirements: 1.3, 1.4
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- 1. Partition function and scheme for audit_logs (monthly range on occurred_at)
--    Covers 2025-01 through 2032-12 (96 months)
-- ─────────────────────────────────────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.partition_functions WHERE name = 'pf_audit_monthly')
BEGIN
    CREATE PARTITION FUNCTION pf_audit_monthly (datetime2(3))
    AS RANGE RIGHT FOR VALUES (
        -- 2025
        '2025-01-01','2025-02-01','2025-03-01','2025-04-01','2025-05-01','2025-06-01',
        '2025-07-01','2025-08-01','2025-09-01','2025-10-01','2025-11-01','2025-12-01',
        -- 2026
        '2026-01-01','2026-02-01','2026-03-01','2026-04-01','2026-05-01','2026-06-01',
        '2026-07-01','2026-08-01','2026-09-01','2026-10-01','2026-11-01','2026-12-01',
        -- 2027
        '2027-01-01','2027-02-01','2027-03-01','2027-04-01','2027-05-01','2027-06-01',
        '2027-07-01','2027-08-01','2027-09-01','2027-10-01','2027-11-01','2027-12-01',
        -- 2028
        '2028-01-01','2028-02-01','2028-03-01','2028-04-01','2028-05-01','2028-06-01',
        '2028-07-01','2028-08-01','2028-09-01','2028-10-01','2028-11-01','2028-12-01',
        -- 2029
        '2029-01-01','2029-02-01','2029-03-01','2029-04-01','2029-05-01','2029-06-01',
        '2029-07-01','2029-08-01','2029-09-01','2029-10-01','2029-11-01','2029-12-01',
        -- 2030
        '2030-01-01','2030-02-01','2030-03-01','2030-04-01','2030-05-01','2030-06-01',
        '2030-07-01','2030-08-01','2030-09-01','2030-10-01','2030-11-01','2030-12-01',
        -- 2031
        '2031-01-01','2031-02-01','2031-03-01','2031-04-01','2031-05-01','2031-06-01',
        '2031-07-01','2031-08-01','2031-09-01','2031-10-01','2031-11-01','2031-12-01',
        -- 2032
        '2032-01-01','2032-02-01','2032-03-01','2032-04-01','2032-05-01','2032-06-01',
        '2032-07-01','2032-08-01','2032-09-01','2032-10-01','2032-11-01','2032-12-01'
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.partition_schemes WHERE name = 'ps_audit_monthly')
BEGIN
    CREATE PARTITION SCHEME ps_audit_monthly
    AS PARTITION pf_audit_monthly ALL TO ([PRIMARY]);
END;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 2. Security: pos_app_role with append-only permissions on audit_logs
-- ─────────────────────────────────────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'pos_app_role' AND type = 'R')
    CREATE ROLE pos_app_role;
GO

GRANT SELECT, INSERT ON dbo.audit_logs TO pos_app_role;
GO

DENY UPDATE, DELETE, ALTER, CONTROL ON dbo.audit_logs TO pos_app_role;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 3. Triggers: prevent UPDATE and DELETE on audit_logs
-- ─────────────────────────────────────────────────────────────────────────────

CREATE OR ALTER TRIGGER tr_audit_logs_no_update ON dbo.audit_logs
INSTEAD OF UPDATE
AS
BEGIN
    THROW 50001, 'Audit log entries cannot be modified', 1;
END;
GO

CREATE OR ALTER TRIGGER tr_audit_logs_no_delete ON dbo.audit_logs
INSTEAD OF DELETE
AS
BEGIN
    THROW 50001, 'Audit log entries cannot be deleted', 1;
END;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 4. READ_COMMITTED_SNAPSHOT isolation for reduced lock contention
-- ─────────────────────────────────────────────────────────────────────────────

ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 5. Conditional Ledger for SQL Server 2022+ (major version >= 16)
--    APPEND_ONLY ledger provides cryptographic verification of immutability
-- ─────────────────────────────────────────────────────────────────────────────

IF CAST(SERVERPROPERTY('ProductMajorVersion') AS INT) >= 16
BEGIN
    EXEC sp_executesql N'ALTER TABLE dbo.audit_logs SET (LEDGER = ON (APPEND_ONLY = ON))';
END;
GO
