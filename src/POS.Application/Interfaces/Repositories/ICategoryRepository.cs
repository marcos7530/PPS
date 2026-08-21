using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface ICategoryRepository : IRepository<Category>
{
    Task<bool> ExistsByNameAndParentAsync(string name, Guid? parentCategoryId, CancellationToken ct = default);
    Task<IReadOnlyList<Category>> GetChildrenAsync(Guid parentId, CancellationToken ct = default);
    Task<IReadOnlyList<Category>> GetRootsAsync(CancellationToken ct = default);
}
