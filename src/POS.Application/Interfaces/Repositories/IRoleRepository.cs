using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface IRoleRepository : IRepository<Role>
{
    Task<Role?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default);
}
