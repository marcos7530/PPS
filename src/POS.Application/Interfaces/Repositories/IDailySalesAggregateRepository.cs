using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface IDailySalesAggregateRepository
{
    Task<IReadOnlyList<DailySalesAggregate>> GetByDateRangeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
    Task<IReadOnlyList<DailySalesAggregate>> GetByCategoryAsync(Guid categoryId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
    Task UpsertAsync(DailySalesAggregate aggregate, CancellationToken ct = default);
}
