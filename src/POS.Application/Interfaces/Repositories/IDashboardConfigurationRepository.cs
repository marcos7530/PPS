using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface IDashboardConfigurationRepository : IRepository<DashboardConfiguration>
{
    Task<DashboardConfiguration?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
