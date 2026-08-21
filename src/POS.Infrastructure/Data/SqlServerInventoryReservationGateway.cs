using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Interfaces.Services;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Infrastructure.Data;

/// <summary>
/// SQL Server implementation of <see cref="IInventoryReservationGateway"/>.
/// Locks product rows with UPDLOCK, ROWLOCK, HOLDLOCK in deterministic order (product_id ASC)
/// to prevent deadlocks, then adjusts stock quantities atomically within the current transaction.
/// </summary>
public sealed class SqlServerInventoryReservationGateway : IInventoryReservationGateway
{
    private readonly PosDbContext _context;

    public SqlServerInventoryReservationGateway(PosDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyDictionary<Guid, int>>> LockAndAdjustAsync(
        IReadOnlyList<StockDelta> deltas, CancellationToken ct)
    {
        if (deltas.Count == 0)
        {
            return Result<IReadOnlyDictionary<Guid, int>>.Success(
                new Dictionary<Guid, int>());
        }

        // Sort by ProductId ASC for deterministic lock ordering (prevents deadlocks)
        var sortedDeltas = deltas.OrderBy(d => d.ProductId).ToList();
        var productIds = sortedDeltas.Select(d => d.ProductId).Distinct().ToList();

        // Build parameterized SQL for locking query
        // EF Core's FromSqlRaw requires parameters for the IN clause
        var parameters = new object[productIds.Count];
        var paramPlaceholders = new string[productIds.Count];
        for (var i = 0; i < productIds.Count; i++)
        {
            paramPlaceholders[i] = $"{{{i}}}";
            parameters[i] = productIds[i];
        }

        var inClause = string.Join(", ", paramPlaceholders);
        var sql = $"SELECT * FROM [dbo].[products] WITH (UPDLOCK, ROWLOCK, HOLDLOCK) WHERE [id] IN ({inClause}) ORDER BY [id]";

        // Execute locking query — entities become tracked by EF Core
        var lockedProducts = await _context.Products
            .FromSqlRaw(sql, parameters)
            .AsTracking()
            .ToListAsync(ct);

        // Build a lookup for quick access
        var productMap = lockedProducts.ToDictionary(p => p.Id);

        // Validate all deltas before applying any changes
        foreach (var delta in sortedDeltas)
        {
            if (!productMap.TryGetValue(delta.ProductId, out var product))
            {
                return Result<IReadOnlyDictionary<Guid, int>>.Failure(
                    DomainError.Create(ErrorCode.InsufficientInventory,
                        "productId", delta.ProductId));
            }

            var newQuantity = product.Quantity + delta.QuantityDelta;
            if (newQuantity < 0)
            {
                return Result<IReadOnlyDictionary<Guid, int>>.Failure(
                    DomainError.Create(ErrorCode.InsufficientInventory,
                        "productId", delta.ProductId));
            }
        }

        // All validations passed — apply stock adjustments
        var updatedLevels = new Dictionary<Guid, int>(productIds.Count);

        foreach (var delta in sortedDeltas)
        {
            var product = productMap[delta.ProductId];
            product.Quantity += delta.QuantityDelta;
            updatedLevels[delta.ProductId] = product.Quantity;
        }

        // The entities are tracked; the caller's SaveChangesAsync (via UnitOfWork)
        // will persist the quantity changes within the same transaction.
        return Result<IReadOnlyDictionary<Guid, int>>.Success(updatedLevels);
    }
}
