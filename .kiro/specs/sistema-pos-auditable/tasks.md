# Implementation Plan: Sistema POS Auditable

## Overview

This plan implements a comprehensive Point of Sale system in .NET 8 with Blazor Server, SQL Server 2022, and EF Core 8. The central differentiator is atomic auditability — every data-modifying operation writes an immutable AuditLog entry within the same database transaction. The implementation follows clean architecture layers (Domain → Application → Infrastructure → Presentation) and covers 20 requirement areas.

## Tasks

- [ ] 1. Set up solution structure, domain layer, and core infrastructure
  - [x] 1.1 Create .NET 8 solution with clean architecture projects
    - Create solution file and projects: `POS.Domain`, `POS.Application`, `POS.Infrastructure`, `POS.Presentation`, `POS.Tests`
    - Configure project references following dependency rule: Presentation → Application → Domain; Infrastructure → Application/Domain
    - Add NuGet packages: EF Core 8, BCrypt.Net-Next, ApexCharts.Blazor, QuestPDF, ClosedXML, ImageSharp, MailKit, Quartz.NET, xUnit, CsCheck, Testcontainers.MsSql
    - Set up global usings, nullable reference types, and code analysis
    - _Requirements: All (project scaffold)_

  - [ ] 1.2 Implement domain value objects and core abstractions
    - Create `Money` value object (decimal with 2-decimal constraint, half-up rounding)
    - Create `Percentage` value object (decimal 0.00-1000.00, 2 decimal places)
    - Create `Barcode` value object with EAN-13, UPC-A, Code 128 validation and check digit verification
    - Create `OperatingDay` value object with timezone conversion from UTC instant
    - Create `Denomination` value object for cash count breakdown (100.00, 50.00, 20.00, 10.00, 5.00, 1.00, 0.25, 0.10, 0.05, 0.01)
    - Create `Result<T>` and `Error` types with `ErrorCode` enum (full catalog from design)
    - Create `IClock` interface (`DateTimeOffset UtcNow`) for time abstraction
    - _Requirements: 9.3, 15.11, 18.2, 18.3, 12.1, 1.1_

  - [ ] 1.3 Implement domain entities and aggregates
    - Create `User`, `Role`, `UserRole` entities with validation rules
    - Create `Session`, `PasswordResetToken` entities
    - Create `Category` entity with depth constraint (1-5) and parent reference
    - Create `Product` entity with SKU, barcode, pricing, stock, and margin fields
    - Create `ProductImage` entity (one per product constraint)
    - Create `Customer`, `StoreCredit`, `StoreCreditVoucher` entities
    - Create `Transaction`, `TransactionLineItem`, `Payment`, `LineItemDiscount`, `TransactionDiscount` entities
    - Create `Return`, `ReturnLineItem` entities
    - Create `Shift`, `CashMovement`, `CashCount` entities
    - Create `AuditLog` entity (immutable, append-only)
    - Create `Receipt`, `ReportSchedule`, `DashboardConfiguration`, `SystemConfiguration` entities
    - Create `DailySalesAggregate` entity
    - _Requirements: 2.1, 2.4, 3.3, 4.1, 9.1, 9.18, 10.1, 11.1, 12.1, 13.1, 14.1, 15.1, 16.1, 17.1, 20.10_

  - [ ] 1.4 Create application layer interfaces (ports)
    - Define `IAuditWriter` with `Enqueue` and `WriteFailedAttemptAsync` methods
    - Define `ISalesService`, `IReturnService`, `IVoidService`, `IShiftService`
    - Define `IInventoryReservationGateway` with locking contract
    - Define `IStoreCreditService` (consume/restore)
    - Define `IMarginService`, `ICategoryTreeService`, `IProductSearchService`
    - Define `IElevationService` for manager authorization without session change
    - Define `IReceiptService`, `IProductImageService`
    - Define `IPasswordHasher`, `IEmailSender`, `IReceiptRenderer`, `IPrinterGateway`
    - Define `IUnitOfWork` and repository interfaces for all entities
    - _Requirements: 1.1, 1.8, 9.1, 11.1, 12.1, 14.1, 15.7, 17.1, 18.6, 19.11, 20.1_

  - [ ]* 1.5 Write property test for monetary calculations (Property 10)
    - **Property 10: No rounding error accumulation**
    - Verify all monetary amounts have exactly 2 decimals, sum of parts equals totals, and idempotence of rounding
    - **Validates: Requirements 9.3, 15.11, 15.23, 19.3, 7.6**

- [ ] 2. Implement database layer with EF Core and SQL Server
  - [ ] 2.1 Create PosDbContext with entity configurations
    - Configure all entity mappings with correct column types (`decimal(18,2)`, `datetime2(3)`, `nvarchar`, `varchar`)
    - Configure collations: `Latin1_General_100_CI_AS` for username, user email, customer email, category name; `Latin1_General_100_CI_AI` for product name, customer name
    - Configure unique indexes, filtered indexes (active shift per drawer, active shift per user, active password reset token, active voucher payment)
    - Configure CHECK constraints (quantity >= 0, final_amount equation, void consistency, variance notes)
    - Configure `IsRowVersion()` for optimistic concurrency on Product and User
    - Configure composite PKs (UserRole, CategoryClosure, DailySalesAggregate)
    - _Requirements: 2.2, 10.8, 10.9, 12.2, 12.3, 13.2, 14.2, 18.4_

  - [ ] 2.2 Create AuditSaveChangesInterceptor
    - Implement `SaveChangesInterceptor` that derives before/after JSON from `ChangeTracker`
    - Insert `AuditLog` entries within the same transaction as the operation
    - On audit INSERT failure, ensure the entire transaction rolls back
    - Handle `WriteFailedAttemptAsync` for recording validation failures with `outcome='failure'`
    - _Requirements: 1.1, 1.2, 1.6, 1.7, 1.8_

  - [ ] 2.3 Create initial EF Core migration with audit immutability DDL
    - Generate migration with all tables, indexes, constraints, and partitioning for `audit_log`
    - Add SQL for `DENY UPDATE, DELETE, ALTER, CONTROL ON dbo.audit_log TO pos_app_role`
    - Add `INSTEAD OF UPDATE, DELETE` trigger on `audit_log` (THROW 50001)
    - Add `CREATE ROLE pos_app_role; GRANT SELECT, INSERT ON dbo.audit_log TO pos_app_role`
    - Add partition function/scheme for monthly range on `occurred_at`
    - Add clustered indexes per design (transaction by operating_day+completed_at, audit_log by occurred_at+id)
    - Configure `READ_COMMITTED_SNAPSHOT ON`
    - Conditionally apply `LEDGER = ON (APPEND_ONLY = ON)` for SQL Server 2022+
    - _Requirements: 1.3, 1.4_

  - [ ] 2.4 Implement SqlServerInventoryReservationGateway
    - Implement locking with `SELECT ... WITH (UPDLOCK, ROWLOCK, HOLDLOCK)` ordered by `product_id ASC`
    - Implement atomic stock adjustment within the current transaction
    - Use `FromSqlInterpolated` for the locking query
    - Return `Result<T>` with appropriate error codes for insufficient stock
    - _Requirements: 9.21, 9.22, 11.13, 20.7_

  - [ ]* 2.5 Write property test for audit immutability (Property 4)
    - **Property 4: No audit entry can be modified or deleted**
    - Test UPDATE, DELETE, TRUNCATE, ALTER TABLE, EF Core ChangeTracker mutations all fail
    - Test with both `pos_app` principal and schema owner principal
    - **Validates: Requirements 1.3**

  - [ ]* 2.6 Write property test for business key uniqueness (Property 15)
    - **Property 15: Business key uniqueness**
    - Verify username, user email, customer email, SKU, barcode, (parent, category name) uniqueness including deactivated entities, with case variations
    - **Validates: Requirements 2.2, 10.8, 10.9, 13.2, 14.2, 18.4, 18.5, 18.18**

- [ ] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 4. Implement authentication, users, and roles
  - [ ] 4.1 Implement BCryptPasswordHasher and AuthenticationService
    - Implement password hashing with BCrypt cost factor 12
    - Implement login with dummy verification for timing equality on invalid usernames
    - Implement session creation with 128-bit cryptographically random token, 8-hour expiration
    - Implement account lockout: 3 failures in 15 min → lock 30 min, auto-unlock on expiry
    - Implement password validation (8-128 chars, uppercase, lowercase, digit, special char)
    - Return identical error message "Invalid credentials" for wrong username and wrong password
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8_

  - [ ] 4.2 Implement UserService with role management
    - Implement user CRUD with username (1-50), email (valid, max 100), password, and roles
    - Enforce duplicate username/email rejection
    - Enforce last administrator protection (UPDLOCK counting on user_role)
    - Enforce cannot remove own administrator role
    - Apply role permission changes on next session
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 5.1-5.8_

  - [ ] 4.3 Implement password recovery flow
    - Generate 128-bit token with 24-hour expiration
    - Send reset URL via MailKit with 3 retry attempts
    - Invalidate previous tokens on new request
    - Validate token, update password hash, invalidate all sessions on success
    - Rate limit: 5 requests per email per hour
    - Same response for existing/non-existing emails
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 4.9, 4.10_

  - [ ]* 4.4 Write property test for authentication cycle (Property 16)
    - **Property 16: Authentication, lockout, and expiration cycle**
    - Model-based test with injected clock: 3 failures in 15 min → lock 30 min, session expires at 8h, identical error messages for invalid user/password
    - **Validates: Requirements 3.2, 3.3, 3.5, 3.6, 3.7, 3.8, 4.9**

  - [ ]* 4.5 Write property test for permission matrix (Property 13)
    - **Property 13: Permission matrix**
    - For every (role, operation) pair, verify access matches the design permission matrix; denied operations produce no state change
    - **Validates: Requirements 2.5, 2.6, 5.1, 5.2, 5.5, 9.2, 11.1, 12.15, 13.11, 15.2, 16.2, 20.2**

- [ ] 5. Implement category hierarchy and profit margins
  - [ ] 5.1 Implement CategoryTreeService with closure table
    - Implement category CRUD with name (1-100), parent, description, display order
    - Maintain `CategoryClosure` table transactionally on create/move/deactivate
    - Validate depth <= 5 on create and move
    - Detect and reject circular references on move
    - Cascade deactivation to all descendants
    - Enforce unique (parent_category_id, name) including root level
    - _Requirements: 14.1, 14.2, 14.3, 14.4, 14.5, 14.6, 14.7, 14.8, 14.9, 14.10, 14.11, 14.12, 14.13, 14.14, 14.15, 14.16, 14.17, 14.18_

  - [ ] 5.2 Implement MarginService with hierarchical resolution
    - Implement effective margin resolution: product > nearest ancestor category > global (using closure table)
    - Calculate `SuggestedPrice = CostPrice × (1 + EffectiveMargin / 100)` with half-up rounding to 2 decimals
    - Support global margin CRUD (Admin only), category margin CRUD (Manager+), product margin CRUD (Manager+)
    - Implement batch price recalculation with confirmation flow (excluding manual overrides and deactivated products)
    - Record margin changes in AuditLog
    - _Requirements: 15.1, 15.2, 15.3, 15.4, 15.5, 15.6, 15.7, 15.8, 15.9, 15.10, 15.11, 15.12, 15.13, 15.14, 15.15, 15.16, 15.17, 15.18, 15.19, 15.20, 15.21_

  - [ ]* 5.3 Write property test for category hierarchy (Property 9)
    - **Property 9: Category hierarchy never has cycles nor exceeds 5 levels**
    - For any sequence of create/move/deactivate, the graph is always a forest with depth ≤ 5, closure table equals transitive closure, and inactive cascades to descendants
    - **Validates: Requirements 14.6, 14.7, 14.9, 14.11**

  - [ ]* 5.4 Write property test for margin precedence (Property 8)
    - **Property 8: Effective profit margin precedence**
    - For any category tree with sparse margins and any product, resolved margin matches naive parent-walk; suggested price uses half-up rounding
    - **Validates: Requirements 15.5, 15.6, 15.7, 15.8, 15.11**

- [ ] 6. Implement inventory and product management
  - [ ] 6.1 Implement InventoryService and product CRUD
    - Implement product creation with name, SKU, description, price, cost price, category, quantity, min stock threshold
    - Enforce SKU uniqueness across all products (including deactivated)
    - Implement product modification and soft deactivation
    - Implement low stock detection (quantity <= min_stock_threshold)
    - Record quantity adjustments in AuditLog with reason (return, damage, correction, restock)
    - Reject transactions with deactivated products
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 10.7, 10.8, 10.9, 10.10_

  - [ ] 6.2 Implement ProductSearchService
    - Implement barcode exact match search (< 1 second)
    - Implement SKU exact match search (< 1 second)
    - Implement product name partial match search (case/accent insensitive via collation, < 2 seconds, top 50)
    - Display "No products found" when zero matches, "Showing 50 of N" when > 50 matches
    - _Requirements: 18.6, 18.7, 18.8, 18.9, 18.10_

  - [ ] 6.3 Implement barcode management
    - Validate barcode format (EAN-13: 13 digits, UPC-A: 12 digits, Code 128: 1-48 printable ASCII)
    - Validate check digit for EAN-13 and UPC-A
    - Enforce barcode uniqueness across all products (including deactivated)
    - Implement Code 128 barcode generation (12 chars) with uniqueness verification
    - Record barcode changes in AuditLog
    - _Requirements: 18.1, 18.2, 18.3, 18.4, 18.5, 18.17, 18.18, 18.19_

  - [ ] 6.4 Implement ProductImageService
    - Validate file by magic bytes (JPEG, PNG, WebP), not extension
    - Enforce size (1-5,242,880 bytes) and dimensions (≤ 4000×4000 pixels)
    - Validate full image decode
    - Generate 200×200 thumbnail preserving aspect ratio with letterbox
    - Enforce one image per product; confirm replacement; delete removes both files
    - Record upload/replace/delete in AuditLog with metadata
    - _Requirements: 16.1-16.15, 16.23, 16.24, 16.25_

  - [ ]* 6.5 Write property test for image validation (Property 17)
    - **Property 17: Image validation by content, not by name**
    - Acceptance depends only on binary content and real dimensions; thumbnail is 200×200; rejection/failure preserves previous image; no orphan files
    - **Validates: Requirements 16.3, 16.4, 16.5, 16.6, 16.9, 16.10, 16.12, 16.13, 16.24**

- [ ] 7. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 8. Implement sales transactions and barcode scanning
  - [ ] 8.1 Implement SalesService (transaction lifecycle)
    - Implement `AddLineItemAsync`: validate product exists/active, check stock, enforce quantity 1-9999
    - Implement `AddByBarcodeAsync`: scan handling per design (new item qty=1, existing item qty+1)
    - Calculate subtotal, tax, discount, final amount with 2-decimal precision
    - Implement `CompleteAsync`: validate payment method, amount received, shift requirement for cash
    - Handle store credit payment (voucher code or customer balance), partial payments
    - Generate UUID v4 transaction identifier
    - Atomically decrement inventory via `IInventoryReservationGateway`
    - Record shift_id, operating_day, and full transaction details in AuditLog
    - _Requirements: 9.1-9.22, 18.11-18.16_

  - [ ] 8.2 Implement StoreCreditService
    - Implement voucher consumption: validate code exists, not used, not expired
    - Apply `min(voucher.amount, final_amount)` with 2-decimal precision
    - Handle partial store credit with additional payment method
    - Implement customer balance consumption with same logic
    - Implement `RestoreAsync` for void scenario (reset voucher to unused/restore balance)
    - Record store credit operations in AuditLog
    - _Requirements: 9.8, 9.9, 9.10, 9.11, 9.12, 9.13, 9.14, 9.15, 20.9_

  - [ ] 8.3 Implement DiscountService
    - Implement line item discount (percentage 0-100 or fixed amount ≤ line amount)
    - Implement transaction total discount (percentage 0-100 or fixed amount ≤ subtotal)
    - Enforce `final_amount >= 0` after discounts
    - Enforce `Discount_Limit` per role (Cashier default 10%, Manager/Admin 100%)
    - Require `Discount_Authorization` via `IElevationService` when exceeding limit
    - Require `Discount_Reason` from predefined list
    - Warn on below-cost-price discount with confirmation
    - Record discount details in AuditLog
    - _Requirements: 19.1-19.18_

  - [ ]* 8.4 Write property test for transaction equation (Property 2)
    - **Property 2: Transaction equation**
    - `final_amount = subtotal + tax - discount`, `final_amount >= 0`, `change_due = received - final >= 0`, all amounts have exactly 2 decimals
    - **Validates: Requirements 9.3, 9.16, 9.17, 19.3, 19.5, 19.6, 19.7**

  - [ ]* 8.5 Write property test for inventory non-negativity (Property 1)
    - **Property 1: Inventory never goes negative**
    - Under concurrent sales/returns/voids, `quantity >= 0` always, stock conservation holds, no deadlocks (error 1205)
    - **Validates: Requirements 9.5, 9.21, 9.22, 10.7, 11.13, 11.14, 18.15, 20.7, 20.18**

  - [ ]* 8.6 Write property test for barcode cart model (Property 18)
    - **Property 18: Cart model under barcode scanning**
    - New code adds line qty=1, existing code increments qty, no duplicate lines, rejections don't modify cart, EAN-13/UPC-A check digit validation
    - **Validates: Requirements 18.3, 18.6, 18.8, 18.11, 18.12, 18.13, 18.14, 18.15, 18.16, 18.17**

  - [ ]* 8.7 Write property test for voucher single-use (Property 6)
    - **Property 6: A store credit voucher is never consumed twice**
    - Concurrent attempts succeed at most once; void restores voucher for exactly one more use; customer balance never goes negative
    - **Validates: Requirements 9.9, 9.10, 9.11, 9.12, 9.13, 9.14, 9.15, 20.9**

- [ ] 9. Implement returns and voids
  - [ ] 9.1 Implement ReturnService
    - Load returnable transaction (must exist, not older than 90 days, not voided)
    - Display original line items with available return quantities
    - Validate return quantity (1 to original qty minus already returned)
    - Calculate refund amount as sum of (return_qty × unit_price)
    - Require refund method (cash, credit card reversal, store credit) and reason code
    - Enforce active shift for cash refunds
    - Require manager authorization for store credit refunds or amounts > 500.00
    - Generate UUID v4 return identifier linked to original transaction
    - Atomically increment inventory for returned products
    - Create Store_Credit balance or Store_Credit_Voucher for store credit refunds
    - Record full return details in AuditLog
    - _Requirements: 11.1-11.16_

  - [ ] 9.2 Implement VoidService
    - Validate: same operating day, shift still open, not already voided, no existing returns
    - Require void reason and notes (1-500 chars)
    - Atomically restore inventory for all line items
    - Subtract cash amount from shift expected balance
    - Restore store credit (voucher to unused or customer balance)
    - Mark transaction as voided (preserve record, line items, receipts)
    - Record void details in AuditLog
    - _Requirements: 20.1-20.19_

  - [ ]* 9.3 Write property test for returned quantity bounds (Property 7)
    - **Property 7: Returned quantity never exceeds sold quantity**
    - For any sequence of partial returns (concurrent), `0 <= returned_quantity <= quantity` always holds
    - **Validates: Requirements 11.4, 11.5, 11.13**

  - [ ]* 9.4 Write property test for discount authorization (Property 14)
    - **Property 14: Discount authorization without session change**
    - Authorization required iff discount % exceeds role limit; valid manager credentials accept; session of applying user remains intact; no session created for authorizer
    - **Validates: Requirements 19.10, 19.11, 19.12, 19.13, 11.10, 11.11**

- [ ] 10. Implement cash shifts
  - [ ] 10.1 Implement ShiftService
    - Implement shift opening: validate one shift per user, one per drawer; record cash count by denomination
    - Implement deposits and withdrawals with amount, reason, notes
    - Calculate expected cash balance: `opening + cash_sales(not voided) + deposits - withdrawals - cash_refunds - voided_cash_sales`
    - Implement shift closing: require cash count, calculate variance, enforce notes for |variance| > 10.00
    - Generate shift summary with all required metrics
    - Freeze expected_cash_balance on close (immutable after)
    - Record all shift operations in AuditLog
    - _Requirements: 12.1-12.15_

  - [ ]* 10.2 Write property test for expected cash equation (Property 5)
    - **Property 5: Shift expected cash equation**
    - For any shift history (sales, deposits, withdrawals, refunds, voids), expected cash matches model; card sales don't affect expected; variance = closing - expected; frozen after close
    - **Validates: Requirements 12.8, 12.10, 12.11, 12.13, 12.14, 9.19, 9.20, 11.9, 20.8**

- [ ] 11. Implement customers
  - [ ] 11.1 Implement CustomerService
    - Implement customer CRUD: name (1-100), email (unique, optional), phone (7-20, warn on duplicate), notes
    - Implement customer search: name (partial, CI/AI), email (exact), phone (partial), identifier
    - Link customers to transactions (optional)
    - Calculate lifetime statistics: total transactions, total purchase amount, last purchase date
    - Soft deactivation (exclude from search, preserve history)
    - Record customer operations in AuditLog
    - _Requirements: 13.1-13.14_

- [ ] 12. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 13. Implement receipts and printing
  - [ ] 13.1 Implement ReceiptService and QuestPDF renderer
    - Generate receipt content with all required fields (tx id, timestamp in business timezone, business info, user, customer, line items, totals, payment, change, store credit details)
    - Render for thermal printer (80mm width) via QuestPDF
    - Render as downloadable PDF (80mm page width)
    - Render return receipts with all required fields
    - Implement Receipt_Reprint with "REPRINT #N" text and count increment
    - Include "VOIDED TRANSACTION" text for voided transactions
    - Include configurable Receipt_Footer_Text
    - Record receipt emissions and reprints in AuditLog
    - _Requirements: 17.1-17.17_

  - [ ] 13.2 Implement EscPosPrinterGateway and email delivery
    - POST to local agent (`localhost:9100/print`) with 5-second timeout
    - On failure: offer retry, PDF download, or continue without receipt (preserve transaction)
    - Implement email receipt delivery via MailKit with 3 retries
    - Validate customer email availability
    - _Requirements: 17.3, 17.4, 17.5, 17.6, 17.12, 17.13_

- [ ] 14. Implement reports and dashboard
  - [ ] 14.1 Implement ReportEngine
    - Accept parameters: date range (max 366 days), category IDs (with recursive child option), user IDs
    - Retrieve and filter transaction/audit data
    - Calculate summary statistics: total sales, transaction count, average value (2 decimal precision)
    - Calculate Gross_Margin and Realized_Margin_Percentage per line item, product, category, transaction
    - Export PDF (max 50,000 rows) and Excel (max 100,000 rows) with warnings
    - Exclude voided transactions from all aggregates
    - Handle empty results with "No data found" message
    - _Requirements: 7.1-7.6, 7.10, 15.22-15.25, 19.19, 19.20, 20.14_

  - [ ] 14.2 Implement scheduled reports with Quartz.NET
    - Implement `ReportSchedule` CRUD: frequency (daily/weekly/monthly), format (PDF/Excel), recipients (1-10 emails)
    - Implement `ScheduledReportJob` in Quartz.NET
    - Email report as attachment via MailKit with 3 retries
    - Log failure and notify user on delivery failure
    - _Requirements: 7.7, 7.8, 7.9_

  - [ ] 14.3 Implement DashboardService and aggregates
    - Implement dashboard configuration: add/remove/reorder widgets (max 8 per user)
    - Implement `DailySalesAggregate` maintenance (refresh job or on-demand)
    - Provide metrics: sales by day (30 days), top 10 products, sales by category
    - Support date range filter (max 366 days), update all widgets within 3 seconds
    - Exclude voided transactions from all metrics
    - Handle error states and empty data per widget
    - _Requirements: 8.1-8.10, 20.15_

  - [ ]* 14.4 Write property test for voided transactions exclusion (Property 11)
    - **Property 11: A voided transaction never appears in totals**
    - Report aggregates, dashboard metrics, customer stats, and discount totals equal values computed without voided transactions; voided transactions still appear in history with void details
    - **Validates: Requirements 20.14, 20.15, 20.16, 7.6, 8.1, 8.4, 13.14, 15.24, 19.19**

- [ ] 15. Implement audit query and operating day logic
  - [ ] 15.1 Implement audit query endpoint
    - Retrieve up to 10,000 entries per query with filtering by date range (max 366 days), user, operation type
    - Indicate total count when results exceed 10,000
    - Return most recent entries first
    - Partition elimination on date range queries
    - _Requirements: 1.4, 1.5_

  - [ ] 15.2 Implement OperatingDay derivation and Quartz background jobs
    - Derive `operating_day` from UTC instant + configured business timezone
    - Persist at transaction/return/shift completion time (immutable after)
    - Implement `UnlockExpiredAccountsJob`, `ExpireVouchersJob`, `PurgeExpiredResetTokensJob`
    - Implement `RefreshDashboardAggregatesJob`
    - Implement monthly partition maintenance job for audit_log
    - _Requirements: 9.19, 20.1, 3.8, 7.7_

  - [ ]* 15.3 Write property test for audit atomicity (Property 3)
    - **Property 3: Audit is exact and atomic**
    - For every write command: success → exactly one audit entry with correct before/after; failure → one entry with same error code; audit write failure → full rollback and state unchanged
    - **Validates: Requirements 1.1, 1.2, 1.6, 1.7, 1.8, 9.15, 10.6, 11.16, 12.7, 13.8, 15.16, 16.23, 17.17, 18.19, 19.18, 20.17**

  - [ ]* 15.4 Write property test for OperatingDay consistency (Property 12)
    - **Property 12: Consistent OperatingDay derivation**
    - For any UTC instant and timezone, operating_day equals date part of local conversion; immutable after persistence; void allowed iff same day and shift open
    - **Validates: Requirements 9.19, 20.1, 20.3, 1.1, 17.1**

- [ ] 16. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 17. Implement Blazor Server presentation layer
  - [ ] 17.1 Create responsive layout and navigation
    - Implement `MainLayout` with horizontal nav ≥768px, vertical stacked <768px
    - Implement `NavMenu` with role-based visibility
    - Ensure all interactive elements have 44×44px touch targets on mobile
    - Implement `ResponsiveTable<T>` (table on desktop, cards on mobile with scroll indicators)
    - Ensure body text ≥16px, headings ≥20px on mobile; form inputs ≥44px height
    - Implement cookie-based authentication with `AuthorizationPolicies`
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7_

  - [ ] 17.2 Create POS transaction page with barcode scanning
    - Implement `BarcodeCaptureField` (focus-permanent field interpreting HID scanner bursts ending with Enter)
    - Implement product search panel (barcode, SKU, name)
    - Implement cart display with line items, quantities, prices, discounts
    - Implement `MoneyInput` / `MoneyDisplay` components with 2-decimal formatting
    - Implement payment flow (method selection, amount entry, change calculation)
    - Implement `ManagerAuthDialog` for discount authorization (no session change)
    - Implement receipt output selection (print, PDF, email, continue without)
    - _Requirements: 9.1-9.22, 18.11-18.16, 19.1-19.18_

  - [ ] 17.3 Create inventory, category, and product pages
    - Implement product list with `ProductThumbnail` (200×200 desktop, 80×80 mobile), low stock indicators, loss indicators
    - Implement product create/edit forms with margin display, suggested price, manual override confirmation
    - Implement category tree browser with expandable hierarchy ordered by display order
    - Implement image upload with preview, replace confirmation, and `Image_Placeholder` fallback
    - Implement barcode assignment and generation UI
    - _Requirements: 10.1-10.10, 14.1-14.18, 15.9-15.21, 16.1-16.25, 18.1-18.5, 18.17_

  - [ ] 17.4 Create shifts, returns, voids, and customer pages
    - Implement `ShiftCashCountForm` with denomination breakdown and calculated total
    - Implement shift open/close flow with variance display and notes requirement
    - Implement return flow: transaction lookup, line selection, refund method, authorization
    - Implement void flow: validation display, reason/notes entry
    - Implement customer CRUD, search, purchase history, lifetime statistics
    - _Requirements: 11.1-11.16, 12.1-12.15, 13.1-13.14, 20.1-20.19_

  - [ ] 17.5 Create dashboard and reports pages
    - Implement `ChartWidget` with ApexCharts.Blazor (line, bar, pie, numeric indicator)
    - Implement drag-and-drop widget configuration (max 8)
    - Implement tooltip with 2-decimal currency / 0-decimal quantities
    - Implement date range filter with max 366 days
    - Implement report parameter form, generation, PDF/Excel download
    - Implement scheduled report management
    - _Requirements: 7.1-7.10, 8.1-8.10_

  - [ ] 17.6 Create admin pages (users, system configuration, audit viewer)
    - Implement user management CRUD with role assignment
    - Implement system configuration: tax rate, currency, business name/address, timezone, global margin, cashier discount limit, receipt footer text
    - Implement audit log viewer with filters (date range, user, operation type) and pagination
    - _Requirements: 2.1-2.8, 5.1-5.8, 1.4, 1.5_

  - [ ] 17.7 Implement ErrorMessageLocalizer and error display
    - Create `Errors.en-US.resx` with exact literal messages from requirements
    - Create `Errors.es-AR.resx` with Spanish translations using same placeholders
    - Implement `ErrorAlert` component with `aria-live="assertive"`
    - Map `ErrorCode` to `EditContext` for inline field validation
    - Format money amounts with culture-aware `ToString("N2", culture)`
    - _Requirements: All error messages across all requirements_

- [ ] 18. Implement test infrastructure and remaining property tests
  - [ ] 18.1 Set up PosTestDb with Testcontainers
    - Create `PosTestDb` helper class using `Testcontainers.MsSql` with `mcr.microsoft.com/mssql/server:2022-latest`
    - Apply migrations, create `pos_owner` and `pos_app` principals
    - Configure `READ_COMMITTED_SNAPSHOT ON`
    - Implement `SnapshotAsync`, `AuditSnapshotAsync`, `CountAuditAsync` helpers
    - Implement `FakeClock : IClock` for temporal tests
    - Implement seed helpers for products, categories, customers, transactions
    - _Requirements: Testing infrastructure for Properties 1-18_

  - [ ]* 18.2 Write conformance tests for error messages
    - Verify `ErrorMessageLocalizer.Format(code, args, "en-US")` produces exact requirement literal for every ErrorCode
    - Verify all ErrorCodes have entries in both cultures
    - Verify placeholder consistency between cultures
    - Verify every ErrorCode is referenced by at least one test
    - _Requirements: All error message requirements_

  - [ ]* 18.3 Write architecture tests
    - Verify no `double`/`float`/`MidpointRounding.ToEven` in monetary calculation paths
    - Verify no `DateTime.Now`/`DateTime.UtcNow` outside `IClock`
    - Verify no concatenated SQL strings
    - Verify dependency rule (Domain has no external references)
    - _Requirements: 9.3, 20.1, security_

- [ ] 19. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties using CsCheck with Testcontainers (real SQL Server)
- Unit tests validate specific examples and edge cases
- The implementation language is C# (.NET 8) as specified in the design document
- All monetary calculations use `decimal` with half-up rounding to 2 decimal places
- All timestamps are UTC `DateTimeOffset` mapped to `datetime2(3)`, sourced from `IClock`
- The audit interceptor is the single most critical piece — it must be correct before other features build on it

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3"] },
    { "id": 2, "tasks": ["1.4", "1.5"] },
    { "id": 3, "tasks": ["2.1", "2.2"] },
    { "id": 4, "tasks": ["2.3", "2.4"] },
    { "id": 5, "tasks": ["2.5", "2.6"] },
    { "id": 6, "tasks": ["4.1", "4.2", "5.1", "18.1"] },
    { "id": 7, "tasks": ["4.3", "4.4", "5.2", "6.1"] },
    { "id": 8, "tasks": ["4.5", "5.3", "5.4", "6.2", "6.3", "6.4"] },
    { "id": 9, "tasks": ["6.5", "8.1", "11.1"] },
    { "id": 10, "tasks": ["8.2", "8.3", "10.1"] },
    { "id": 11, "tasks": ["8.4", "8.5", "8.6", "8.7", "10.2"] },
    { "id": 12, "tasks": ["9.1", "9.2"] },
    { "id": 13, "tasks": ["9.3", "9.4", "13.1"] },
    { "id": 14, "tasks": ["13.2", "14.1", "15.1", "15.2"] },
    { "id": 15, "tasks": ["14.2", "14.3", "15.3", "15.4"] },
    { "id": 16, "tasks": ["14.4"] },
    { "id": 17, "tasks": ["17.1", "17.7"] },
    { "id": 18, "tasks": ["17.2", "17.3", "17.4", "17.5", "17.6"] },
    { "id": 19, "tasks": ["18.2", "18.3"] }
  ]
}
```
