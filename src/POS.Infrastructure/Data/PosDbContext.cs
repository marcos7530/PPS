using POS.Domain.Entities;

namespace POS.Infrastructure.Data;

public class PosDbContext : DbContext
{
    public PosDbContext(DbContextOptions<PosDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CategoryClosure> CategoryClosures => Set<CategoryClosure>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionLineItem> TransactionLineItems => Set<TransactionLineItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<LineItemDiscount> LineItemDiscounts => Set<LineItemDiscount>();
    public DbSet<TransactionDiscount> TransactionDiscounts => Set<TransactionDiscount>();
    public DbSet<Return> Returns => Set<Return>();
    public DbSet<ReturnLineItem> ReturnLineItems => Set<ReturnLineItem>();
    public DbSet<StoreCredit> StoreCredits => Set<StoreCredit>();
    public DbSet<StoreCreditVoucher> StoreCreditVouchers => Set<StoreCreditVoucher>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<CashMovement> CashMovements => Set<CashMovement>();
    public DbSet<CashCount> CashCounts => Set<CashCount>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReportSchedule> ReportSchedules => Set<ReportSchedule>();
    public DbSet<DashboardConfiguration> DashboardConfigurations => Set<DashboardConfiguration>();
    public DbSet<DailySalesAggregate> DailySalesAggregates => Set<DailySalesAggregate>();
    public DbSet<SystemConfiguration> SystemConfigurations => Set<SystemConfiguration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PosDbContext).Assembly);
    }
}
