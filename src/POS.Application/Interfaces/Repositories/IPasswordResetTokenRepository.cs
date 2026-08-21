using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface IPasswordResetTokenRepository : IRepository<PasswordResetToken>
{
    Task<PasswordResetToken?> GetActiveByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default);
    Task InvalidateAllForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Counts the number of tokens created for a user since the specified time (for rate limiting).
    /// </summary>
    Task<int> CountRecentByUserAsync(Guid userId, DateTimeOffset since, CancellationToken ct = default);
}
