using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface IReturnRepository : IRepository<Return>
{
    Task<IReadOnlyList<Return>> GetByOriginalTransactionIdAsync(Guid transactionId, CancellationToken ct = default);
    Task<Return?> GetWithLineItemsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Return>> GetByShiftIdAsync(Guid shiftId, CancellationToken ct = default);
}
