using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface IPasswordResetTokenRepository : IRepository<PasswordResetToken>
{
    Task<PasswordResetToken?> GetActiveByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default);
    Task InvalidateAllForUserAsync(Guid userId, CancellationToken ct = default);
}
