using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface ICashCountRepository
{
    Task AddRangeAsync(IEnumerable<CashCount> counts, CancellationToken ct = default);
    Task<IReadOnlyList<CashCount>> GetByShiftIdAsync(Guid shiftId, CancellationToken ct = default);
}
