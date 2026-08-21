using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface ICategoryClosureRepository
{
    Task<IReadOnlyList<CategoryClosure>> GetAncestorsAsync(Guid descendantId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(Guid ancestorId, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<CategoryClosure> closures, CancellationToken ct = default);
    Task RemoveSubtreeAsync(Guid descendantId, CancellationToken ct = default);
}
