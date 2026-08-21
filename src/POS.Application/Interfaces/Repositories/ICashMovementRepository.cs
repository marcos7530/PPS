using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface ICashMovementRepository : IRepository<CashMovement>
{
    Task<IReadOnlyList<CashMovement>> GetByShiftIdAsync(Guid shiftId, CancellationToken ct = default);
}
