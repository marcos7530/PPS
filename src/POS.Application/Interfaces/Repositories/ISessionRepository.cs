using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface ISessionRepository : IRepository<Session>
{
    Task<Session?> GetByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default);
    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
}
