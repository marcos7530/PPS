using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface ITransactionRepository : IRepository<Transaction>
{
    Task<Transaction?> GetWithLineItemsAsync(Guid id, CancellationToken ct = default);
    Task<Transaction?> GetWithPaymentsAsync(Guid id, CancellationToken ct = default);
    Task<Transaction?> GetFullAsync(Guid id, CancellationToken ct = default);
    Task<long> GetNextTransactionNumberAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetByShiftIdAsync(Guid shiftId, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetByOperatingDayAsync(DateOnly operatingDay, CancellationToken ct = default);
}
