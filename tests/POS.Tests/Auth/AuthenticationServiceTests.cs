using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Infrastructure;
using POS.Application.Interfaces.Repositories;
using POS.Application.Services;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Tests.Auth;

/// <summary>
/// Unit tests for AuthenticationService (Req 3.1–3.8).
/// </summary>
public class AuthenticationServiceTests : IDisposable
{
    private readonly FakeUserRepository _userRepo = new();
    private readonly FakeSessionRepository _sessionRepo = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeClock _clock = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAuditWriter _auditWriter = new();
    private readonly AuthenticationService _sut;

    public AuthenticationServiceTests()
    {
        _sut = new AuthenticationService(
            _userRepo,
            _sessionRepo,
            _hasher,
            _clock,
            _unitOfWork,
            _auditWriter);
    }

    public void Dispose()
    {
        _unitOfWork.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsSuccess()
    {
        var user = CreateUser("admin", "hashed_password");
        _userRepo.Users.Add(user);
        _hasher.VerifyResult = true;

        var request = new LoginRequest("admin", "correct_password", "127.0.0.1", "TestAgent");
        var result = await _sut.LoginAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value!.UserId);
        Assert.Equal("admin", result.Value.Username);
        Assert.NotNull(result.Value.RawToken);
        Assert.Equal(_clock.UtcNow.AddHours(8), result.Value.ExpiresAt);
    }

    [Fact]
    public async Task Login_WithInvalidUsername_ReturnsInvalidCredentials()
    {
        // No users in repository
        var request = new LoginRequest("nonexistent", "password", "127.0.0.1", null);
        var result = await _sut.LoginAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidCredentials, result.Error!.Value.Code);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsInvalidCredentials()
    {
        var user = CreateUser("admin", "hashed_password");
        _userRepo.Users.Add(user);
        _hasher.VerifyResult = false;

        var request = new LoginRequest("admin", "wrong_password", "127.0.0.1", null);
        var result = await _sut.LoginAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidCredentials, result.Error!.Value.Code);
    }

    [Fact]
    public async Task Login_AfterThreeFailedAttempts_LocksAccount()
    {
        var user = CreateUser("admin", "hashed_password");
        _userRepo.Users.Add(user);
        _hasher.VerifyResult = false;

        var request = new LoginRequest("admin", "wrong", "127.0.0.1", null);

        // Three failed attempts within 15 min window
        await _sut.LoginAsync(request);
        await _sut.LoginAsync(request);
        var result = await _sut.LoginAsync(request);

        // Third attempt should trigger lock
        Assert.NotNull(user.LockedUntil);
        Assert.Equal(_clock.UtcNow.AddMinutes(30), user.LockedUntil.Value);
    }

    [Fact]
    public async Task Login_LockedAccount_ReturnsAccountLocked()
    {
        var user = CreateUser("admin", "hashed_password");
        user.LockedUntil = _clock.UtcNow.AddMinutes(15); // Still locked for 15 more minutes
        _userRepo.Users.Add(user);
        _hasher.VerifyResult = true; // Even with correct password

        var request = new LoginRequest("admin", "correct", "127.0.0.1", null);
        var result = await _sut.LoginAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.AccountLocked, result.Error!.Value.Code);
    }

    [Fact]
    public async Task Login_ExpiredLockout_AllowsLogin()
    {
        var user = CreateUser("admin", "hashed_password");
        user.LockedUntil = _clock.UtcNow.AddMinutes(-1); // Lockout expired 1 minute ago
        user.FailedLoginCount = 3;
        user.FailedWindowStartedAt = _clock.UtcNow.AddMinutes(-45);
        _userRepo.Users.Add(user);
        _hasher.VerifyResult = true;

        var request = new LoginRequest("admin", "correct", "127.0.0.1", null);
        var result = await _sut.LoginAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Null(user.LockedUntil); // Lock cleared
        Assert.Equal(0, user.FailedLoginCount); // Counter reset
    }

    [Fact]
    public async Task Login_FailedWindowExpired_ResetsCounter()
    {
        var user = CreateUser("admin", "hashed_password");
        user.FailedLoginCount = 2;
        user.FailedWindowStartedAt = _clock.UtcNow.AddMinutes(-16); // Window expired (>15 min)
        _userRepo.Users.Add(user);
        _hasher.VerifyResult = false;

        var request = new LoginRequest("admin", "wrong", "127.0.0.1", null);
        await _sut.LoginAsync(request);

        // Should have started a new window with count 1, not 3
        Assert.Equal(1, user.FailedLoginCount);
        Assert.Null(user.LockedUntil); // Not locked (only 1 failure in new window)
    }

    [Fact]
    public async Task Login_Success_CreatesSessionWith128BitToken()
    {
        var user = CreateUser("admin", "hashed_password");
        _userRepo.Users.Add(user);
        _hasher.VerifyResult = true;

        var request = new LoginRequest("admin", "correct", "127.0.0.1", "Mozilla/5.0");
        var result = await _sut.LoginAsync(request);

        Assert.True(result.IsSuccess);
        // Token should be base64 of 16 bytes (128-bit)
        var tokenBytes = Convert.FromBase64String(result.Value!.RawToken);
        Assert.Equal(16, tokenBytes.Length);

        // Session should be stored with hash
        Assert.Single(_sessionRepo.Sessions);
        var session = _sessionRepo.Sessions[0];
        Assert.Equal(32, session.TokenHash.Length); // SHA-256 = 32 bytes
    }

    [Fact]
    public async Task Login_Success_AuditEntryEnqueued()
    {
        var user = CreateUser("admin", "hashed_password");
        _userRepo.Users.Add(user);
        _hasher.VerifyResult = true;

        var request = new LoginRequest("admin", "correct", "127.0.0.1", null);
        await _sut.LoginAsync(request);

        Assert.Single(_auditWriter.EnqueuedDrafts);
        Assert.Equal("Login", _auditWriter.EnqueuedDrafts[0].OperationType);
    }

    [Fact]
    public async Task Login_InvalidUsername_WritesFailedAttemptAudit()
    {
        var request = new LoginRequest("ghost", "password", "127.0.0.1", null);
        await _sut.LoginAsync(request);

        Assert.Single(_auditWriter.FailedAttempts);
        Assert.Equal(ErrorCode.InvalidCredentials, _auditWriter.FailedAttempts[0].code);
    }

    // --- Password Validation Tests (Req 3.4) ---

    [Theory]
    [InlineData("Abc1!xyz")] // 8 chars, meets all
    [InlineData("LongPassword1!LongPassword1!LongPassword1!")] // long, valid
    public void ValidatePassword_ValidPasswords_ReturnsSuccess(string password)
    {
        var result = AuthenticationService.ValidatePassword(password);
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("")] // empty
    [InlineData("Ab1!")] // too short (< 8)
    [InlineData("abcdefgh1!")] // no uppercase
    [InlineData("ABCDEFGH1!")] // no lowercase
    [InlineData("Abcdefgh!")] // no digit
    [InlineData("Abcdefgh1")] // no special char
    public void ValidatePassword_InvalidPasswords_ReturnsFailure(string password)
    {
        var result = AuthenticationService.ValidatePassword(password);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.PasswordRequirementsNotMet, result.Error!.Value.Code);
    }

    [Fact]
    public void ValidatePassword_TooLong_ReturnsFailure()
    {
        var password = "Ab1!" + new string('x', 125); // 129 chars
        var result = AuthenticationService.ValidatePassword(password);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ValidatePassword_ExactlyMaxLength_ReturnsSuccess()
    {
        var password = "Ab1!" + new string('x', 124); // 128 chars
        var result = AuthenticationService.ValidatePassword(password);
        Assert.True(result.IsSuccess);
    }

    // --- Helpers ---

    private static User CreateUser(string username, string passwordHash) => new()
    {
        Id = Guid.NewGuid(),
        Username = username,
        Email = $"{username}@test.com",
        PasswordHash = passwordHash,
        FullName = "Test User",
        IsActive = true,
        FailedLoginCount = 0,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        RowVersion = Array.Empty<byte>()
    };
}

// --- Fakes ---

internal sealed class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = new();

    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => Task.FromResult(Users.FirstOrDefault(u =>
            u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)));

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => Task.FromResult(Users.FirstOrDefault(u =>
            u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default)
        => Task.FromResult(Users.Any(u =>
            u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
        => Task.FromResult(Users.Any(u =>
            u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> ExistsByUsernameAsync(string username, Guid excludeUserId, CancellationToken ct = default)
        => Task.FromResult(Users.Any(u =>
            u.Id != excludeUserId &&
            u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> ExistsByEmailAsync(string email, Guid excludeUserId, CancellationToken ct = default)
        => Task.FromResult(Users.Any(u =>
            u.Id != excludeUserId &&
            u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));

    public Task<User?> GetByIdWithRolesAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(Users.FirstOrDefault(u => u.Id == userId));

    public Task<int> CountAdministratorsWithLockAsync(CancellationToken ct = default)
        => Task.FromResult(Users.Count(u => u.UserRoles.Any(ur => ur.RoleId == Role.WellKnown.AdministratorId)));

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<User>>(Users.AsReadOnly());

    public Task AddAsync(User entity, CancellationToken ct = default)
    {
        Users.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(User entity) { /* no-op for fake */ }
    public void Remove(User entity) => Users.Remove(entity);
}

internal sealed class FakeSessionRepository : ISessionRepository
{
    public List<Session> Sessions { get; } = new();

    public Task<Session?> GetByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default)
        => Task.FromResult(Sessions.FirstOrDefault(s => s.TokenHash.SequenceEqual(tokenHash)));

    public Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        foreach (var s in Sessions.Where(s => s.UserId == userId))
            s.RevokedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Sessions.FirstOrDefault(s => s.Id == id));

    public Task<IReadOnlyList<Session>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Session>>(Sessions.AsReadOnly());

    public Task AddAsync(Session entity, CancellationToken ct = default)
    {
        Sessions.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(Session entity) { /* no-op */ }
    public void Remove(Session entity) => Sessions.Remove(entity);
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public bool VerifyResult { get; set; } = true;

    public string Hash(string plainPassword) => $"hashed_{plainPassword}";
    public bool Verify(string plainPassword, string passwordHash) => VerifyResult;
}

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2024, 6, 15, 10, 0, 0, TimeSpan.Zero);
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class FakeAuditWriter : IAuditWriter
{
    public List<AuditEntryDraft> EnqueuedDrafts { get; } = new();
    public List<(ErrorCode code, AuditContext ctx)> FailedAttempts { get; } = new();

    public void Enqueue(AuditEntryDraft draft) => EnqueuedDrafts.Add(draft);

    public Task WriteFailedAttemptAsync(ErrorCode code, AuditContext ctx, CancellationToken ct)
    {
        FailedAttempts.Add((code, ctx));
        return Task.CompletedTask;
    }
}
