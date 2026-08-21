using POS.Application.Common;
using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Infrastructure;
using POS.Application.Interfaces.Repositories;
using POS.Application.Services;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Tests.Services;

/// <summary>
/// Unit tests for PasswordRecoveryService (Req 4.1–4.10).
/// </summary>
public class PasswordRecoveryServiceTests : IDisposable
{
    private readonly FakePwdRecoveryUserRepository _userRepo = new();
    private readonly FakePwdRecoveryTokenRepository _tokenRepo = new();
    private readonly FakePwdRecoverySessionRepository _sessionRepo = new();
    private readonly FakePwdRecoveryEmailSender _emailSender = new();
    private readonly FakePwdRecoveryPasswordHasher _hasher = new();
    private readonly FakePwdRecoveryClock _clock = new();
    private readonly FakePwdRecoveryUnitOfWork _unitOfWork = new();
    private readonly FakePwdRecoveryAuditWriter _auditWriter = new();
    private readonly PasswordRecoveryService _sut;

    public PasswordRecoveryServiceTests()
    {
        _sut = new PasswordRecoveryService(
            _userRepo,
            _tokenRepo,
            _sessionRepo,
            _emailSender,
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

    // --- RequestResetAsync Tests ---

    [Fact]
    public async Task RequestReset_ExistingEmail_GeneratesTokenAndSendsEmail()
    {
        var user = CreateUser("admin", "admin@test.com");
        _userRepo.Users.Add(user);

        var result = await _sut.RequestResetAsync("admin@test.com");

        Assert.True(result.IsSuccess);
        Assert.Single(_tokenRepo.Tokens);
        var token = _tokenRepo.Tokens[0];
        Assert.Equal(user.Id, token.UserId);
        Assert.Equal(32, token.TokenHash.Length); // SHA-256 = 32 bytes
        Assert.Equal(_clock.UtcNow.AddHours(24), token.ExpiresAt);
        Assert.Single(_emailSender.SentEmails);
        Assert.Contains("admin@test.com", _emailSender.SentEmails[0].Recipients);
    }

    [Fact]
    public async Task RequestReset_NonExistentEmail_ReturnsSameSuccessResponse()
    {
        // No users in repository
        var result = await _sut.RequestResetAsync("nonexistent@test.com");

        Assert.True(result.IsSuccess);
        Assert.Empty(_tokenRepo.Tokens); // No token created
        Assert.Empty(_emailSender.SentEmails); // No email sent
    }

    [Fact]
    public async Task RequestReset_RateLimit_SixthRequestReturnsSameResponse()
    {
        var user = CreateUser("admin", "admin@test.com");
        _userRepo.Users.Add(user);
        _tokenRepo.RecentCountOverride = 5; // Already at limit

        var result = await _sut.RequestResetAsync("admin@test.com");

        Assert.True(result.IsSuccess); // Same response, no reveal
        Assert.Empty(_tokenRepo.Tokens); // No new token
        Assert.Empty(_emailSender.SentEmails); // No email sent
    }

    [Fact]
    public async Task RequestReset_InvalidatesPreviousTokens()
    {
        var user = CreateUser("admin", "admin@test.com");
        _userRepo.Users.Add(user);

        // Add existing token
        var existingToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = new byte[32],
            CreatedAt = _clock.UtcNow.AddHours(-1),
            ExpiresAt = _clock.UtcNow.AddHours(23)
        };
        _tokenRepo.Tokens.Add(existingToken);

        await _sut.RequestResetAsync("admin@test.com");

        Assert.True(_tokenRepo.InvalidatedAllForUser);
    }

    [Fact]
    public async Task RequestReset_EmailSendFailsAfterRetries_ReturnsResetEmailSendFailed()
    {
        var user = CreateUser("admin", "admin@test.com");
        _userRepo.Users.Add(user);
        _emailSender.ShouldThrow = true;

        var result = await _sut.RequestResetAsync("admin@test.com");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.ResetEmailSendFailed, result.Error!.Value.Code);
        Assert.Equal(3, _emailSender.AttemptCount); // 3 retries attempted
    }

    [Fact]
    public async Task RequestReset_EnqueuesAuditEntry()
    {
        var user = CreateUser("admin", "admin@test.com");
        _userRepo.Users.Add(user);

        await _sut.RequestResetAsync("admin@test.com");

        Assert.Single(_auditWriter.EnqueuedDrafts);
        Assert.Equal("PasswordResetRequested", _auditWriter.EnqueuedDrafts[0].OperationType);
    }

    // --- ResetPasswordAsync Tests ---

    [Fact]
    public async Task ResetPassword_ValidToken_UpdatesPasswordAndInvalidatesSessions()
    {
        var user = CreateUser("admin", "admin@test.com");
        _userRepo.Users.Add(user);

        var (rawToken, tokenHash) = GenerateToken();
        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedAt = _clock.UtcNow.AddHours(-1),
            ExpiresAt = _clock.UtcNow.AddHours(23)
        };
        _tokenRepo.Tokens.Add(resetToken);

        // Add an active session
        var session = new Session
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = new byte[32],
            CreatedAt = _clock.UtcNow.AddHours(-2),
            ExpiresAt = _clock.UtcNow.AddHours(6)
        };
        _sessionRepo.Sessions.Add(session);

        var rawTokenBase64 = Convert.ToBase64String(rawToken);
        var result = await _sut.ResetPasswordAsync(rawTokenBase64, "NewPass1!");

        Assert.True(result.IsSuccess);
        Assert.Equal("hashed_NewPass1!", user.PasswordHash); // Password updated
        Assert.NotNull(resetToken.ConsumedAt); // Token consumed
        Assert.NotNull(session.RevokedAt); // Session revoked
    }

    [Fact]
    public async Task ResetPassword_ExpiredToken_ReturnsInvalidOrExpiredResetToken()
    {
        var user = CreateUser("admin", "admin@test.com");
        _userRepo.Users.Add(user);

        var (rawToken, tokenHash) = GenerateToken();
        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedAt = _clock.UtcNow.AddHours(-25),
            ExpiresAt = _clock.UtcNow.AddHours(-1) // Expired
        };
        _tokenRepo.Tokens.Add(resetToken);

        var rawTokenBase64 = Convert.ToBase64String(rawToken);
        var result = await _sut.ResetPasswordAsync(rawTokenBase64, "NewPass1!");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidOrExpiredResetToken, result.Error!.Value.Code);
    }

    [Fact]
    public async Task ResetPassword_ConsumedToken_ReturnsInvalidOrExpiredResetToken()
    {
        var user = CreateUser("admin", "admin@test.com");
        _userRepo.Users.Add(user);

        var (rawToken, tokenHash) = GenerateToken();
        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedAt = _clock.UtcNow.AddHours(-1),
            ExpiresAt = _clock.UtcNow.AddHours(23),
            ConsumedAt = _clock.UtcNow.AddMinutes(-30) // Already consumed
        };
        _tokenRepo.Tokens.Add(resetToken);

        var rawTokenBase64 = Convert.ToBase64String(rawToken);
        var result = await _sut.ResetPasswordAsync(rawTokenBase64, "NewPass1!");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidOrExpiredResetToken, result.Error!.Value.Code);
    }

    [Fact]
    public async Task ResetPassword_InvalidPassword_ReturnsPasswordRequirementsNotMet()
    {
        var user = CreateUser("admin", "admin@test.com");
        _userRepo.Users.Add(user);

        var (rawToken, tokenHash) = GenerateToken();
        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedAt = _clock.UtcNow.AddHours(-1),
            ExpiresAt = _clock.UtcNow.AddHours(23)
        };
        _tokenRepo.Tokens.Add(resetToken);

        var rawTokenBase64 = Convert.ToBase64String(rawToken);
        var result = await _sut.ResetPasswordAsync(rawTokenBase64, "weak");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.PasswordRequirementsNotMet, result.Error!.Value.Code);
    }

    [Fact]
    public async Task ResetPassword_NonExistentToken_ReturnsInvalidOrExpiredResetToken()
    {
        var rawToken = new byte[16];
        Random.Shared.NextBytes(rawToken);
        var rawTokenBase64 = Convert.ToBase64String(rawToken);

        var result = await _sut.ResetPasswordAsync(rawTokenBase64, "NewPass1!");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidOrExpiredResetToken, result.Error!.Value.Code);
    }

    [Fact]
    public async Task ResetPassword_InvalidBase64Token_ReturnsInvalidOrExpiredResetToken()
    {
        var result = await _sut.ResetPasswordAsync("!!!not-base64!!!", "NewPass1!");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.InvalidOrExpiredResetToken, result.Error!.Value.Code);
    }

    [Fact]
    public async Task ResetPassword_EnqueuesAuditEntry()
    {
        var user = CreateUser("admin", "admin@test.com");
        _userRepo.Users.Add(user);

        var (rawToken, tokenHash) = GenerateToken();
        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedAt = _clock.UtcNow.AddHours(-1),
            ExpiresAt = _clock.UtcNow.AddHours(23)
        };
        _tokenRepo.Tokens.Add(resetToken);

        var rawTokenBase64 = Convert.ToBase64String(rawToken);
        await _sut.ResetPasswordAsync(rawTokenBase64, "NewPass1!");

        Assert.Single(_auditWriter.EnqueuedDrafts);
        Assert.Equal("PasswordReset", _auditWriter.EnqueuedDrafts[0].OperationType);
    }

    // --- Helpers ---

    private static User CreateUser(string username, string email) => new()
    {
        Id = Guid.NewGuid(),
        Username = username,
        Email = email,
        PasswordHash = "old_hash",
        FullName = "Test User",
        IsActive = true,
        FailedLoginCount = 0,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        RowVersion = Array.Empty<byte>()
    };

    private static (byte[] RawToken, byte[] TokenHash) GenerateToken()
    {
        var rawToken = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        var tokenHash = System.Security.Cryptography.SHA256.HashData(rawToken);
        return (rawToken, tokenHash);
    }
}

// --- Fakes (internal to avoid naming conflicts with other test files) ---

internal sealed class FakePwdRecoveryUserRepository : IUserRepository
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
        => Task.FromResult(0);

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

internal sealed class FakePwdRecoveryTokenRepository : IPasswordResetTokenRepository
{
    public List<PasswordResetToken> Tokens { get; } = new();
    public bool InvalidatedAllForUser { get; private set; }
    public int? RecentCountOverride { get; set; }

    public Task<PasswordResetToken?> GetActiveByTokenHashAsync(byte[] tokenHash, CancellationToken ct = default)
        => Task.FromResult(Tokens.FirstOrDefault(t => t.TokenHash.SequenceEqual(tokenHash)));

    public Task InvalidateAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        InvalidatedAllForUser = true;
        foreach (var t in Tokens.Where(t => t.UserId == userId))
            t.InvalidatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<int> CountRecentByUserAsync(Guid userId, DateTimeOffset since, CancellationToken ct = default)
    {
        if (RecentCountOverride.HasValue)
            return Task.FromResult(RecentCountOverride.Value);
        return Task.FromResult(Tokens.Count(t => t.UserId == userId && t.CreatedAt >= since));
    }

    public Task<PasswordResetToken?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Tokens.FirstOrDefault(t => t.Id == id));

    public Task<IReadOnlyList<PasswordResetToken>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PasswordResetToken>>(Tokens.AsReadOnly());

    public Task AddAsync(PasswordResetToken entity, CancellationToken ct = default)
    {
        Tokens.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(PasswordResetToken entity) { /* no-op */ }
    public void Remove(PasswordResetToken entity) => Tokens.Remove(entity);
}

internal sealed class FakePwdRecoverySessionRepository : ISessionRepository
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

internal sealed class FakePwdRecoveryEmailSender : IEmailSender
{
    public List<(string Subject, string Body, IReadOnlyList<string> Recipients)> SentEmails { get; } = new();
    public bool ShouldThrow { get; set; }
    public int AttemptCount { get; private set; }

    public Task SendAsync(string subject, string body, IReadOnlyList<string> recipients,
        IReadOnlyList<EmailAttachment>? attachments = null, CancellationToken ct = default)
    {
        AttemptCount++;
        if (ShouldThrow)
            throw new InvalidOperationException("Email send failed");

        SentEmails.Add((subject, body, recipients));
        return Task.CompletedTask;
    }
}

internal sealed class FakePwdRecoveryPasswordHasher : IPasswordHasher
{
    public string Hash(string plainPassword) => $"hashed_{plainPassword}";
    public bool Verify(string plainPassword, string passwordHash) => true;
}

internal sealed class FakePwdRecoveryClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2024, 6, 15, 10, 0, 0, TimeSpan.Zero);
}

internal sealed class FakePwdRecoveryUnitOfWork : IUnitOfWork
{
    public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class FakePwdRecoveryAuditWriter : IAuditWriter
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
