namespace POS.Application.Interfaces.Repositories;

/// <summary>
/// Port for transactional unit of work (Req 1.1, 1.8).
/// Wraps a database transaction for atomic persistence of business operations and audit entries.
/// </summary>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Begins a new database transaction.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Persists all tracked changes to the database.
    /// The AuditSaveChangesInterceptor materializes enqueued audit entries in this call.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Commits the active transaction.
    /// </summary>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>
    /// Rolls back the active transaction.
    /// </summary>
    Task RollbackAsync(CancellationToken ct = default);
}
