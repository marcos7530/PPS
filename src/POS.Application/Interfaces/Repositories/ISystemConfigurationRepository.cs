using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface ISystemConfigurationRepository
{
    Task<SystemConfiguration> GetAsync(CancellationToken ct = default);
    void Update(SystemConfiguration config);
}
