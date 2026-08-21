using POS.Infrastructure.Auth;

namespace POS.Tests.Auth;

/// <summary>
/// Unit tests for BCryptPasswordHasher (Req 3.1).
/// </summary>
public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_ReturnsNonEmptyBcryptString()
    {
        var hash = _hasher.Hash("TestPassword1!");

        Assert.NotNull(hash);
        Assert.StartsWith("$2", hash); // BCrypt prefix
    }

    [Fact]
    public void Hash_UsesCostFactor12()
    {
        var hash = _hasher.Hash("TestPassword1!");

        // BCrypt format: $2a$12$... or $2b$12$...
        Assert.Contains("$12$", hash);
    }

    [Fact]
    public void Verify_ReturnsTrueForMatchingPassword()
    {
        var password = "SecureP@ss1";
        var hash = _hasher.Hash(password);

        Assert.True(_hasher.Verify(password, hash));
    }

    [Fact]
    public void Verify_ReturnsFalseForNonMatchingPassword()
    {
        var hash = _hasher.Hash("CorrectPass1!");

        Assert.False(_hasher.Verify("WrongPass1!", hash));
    }

    [Fact]
    public void Hash_ProducesDifferentHashesForSamePassword()
    {
        var password = "SamePassword1!";
        var hash1 = _hasher.Hash(password);
        var hash2 = _hasher.Hash(password);

        // BCrypt uses random salt, so hashes differ
        Assert.NotEqual(hash1, hash2);

        // Both should verify correctly
        Assert.True(_hasher.Verify(password, hash1));
        Assert.True(_hasher.Verify(password, hash2));
    }
}
