using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface IShiftRepository : IRepository<Shift>
{
    Task<Shift?> GetActiveByDrawerIdAsync(string cashDrawerId, CancellationToken ct = default);
    Task<Shift?> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Shift?> GetWithMovementsAsync(Guid id, CancellationToken ct = default);
}
