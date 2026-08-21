namespace POS.Application.Interfaces.Infrastructure;

/// <summary>
/// Port for password hashing and verification (BCrypt cost 12).
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a plaintext password using BCrypt with cost factor 12.
    /// </summary>
    string Hash(string plainPassword);

    /// <summary>
    /// Verifies a plaintext password against a BCrypt hash.
    /// Implementations must use constant-time comparison.
    /// </summary>
    bool Verify(string plainPassword, string passwordHash);
}
