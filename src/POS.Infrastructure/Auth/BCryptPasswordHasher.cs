using POS.Application.Interfaces.Infrastructure;

namespace POS.Infrastructure.Auth;

/// <summary>
/// BCrypt password hasher with cost factor 12 (Req 3.1).
/// Uses constant-time comparison internally via BCrypt.Net library.
/// </summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    /// <inheritdoc />
    public string Hash(string plainPassword)
    {
        return BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: WorkFactor);
    }

    /// <inheritdoc />
    public bool Verify(string plainPassword, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(plainPassword, passwordHash);
    }
}
