using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using POS.Application.Interfaces.Repositories;

namespace POS.Infrastructure.Data;

/// <summary>
/// Wraps <see cref="PosDbContext"/> transactions.
/// The <see cref="AuditSaveChangesInterceptor"/> materializes enqueued audit entries
/// during <see cref="SaveChangesAsync"/>, ensuring they persist in the same transaction.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly PosDbContext _context;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(PosDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("A transaction is already active.");

        _transaction = await _context.Database.BeginTransactionAsync(ct);
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        // The AuditSaveChangesInterceptor hooks into this call to materialize
        // enqueued audit drafts into AuditLog entities within the same transaction.
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No active transaction to commit.");

        await _transaction.CommitAsync(ct);
        await DisposeTransactionAsync();
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No active transaction to rollback.");

        await _transaction.RollbackAsync(ct);
        await DisposeTransactionAsync();
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeTransactionAsync();
    }

    private async Task DisposeTransactionAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
