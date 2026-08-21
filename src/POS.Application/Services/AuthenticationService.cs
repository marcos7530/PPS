using System.Security.Cryptography;
using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Infrastructure;
using POS.Application.Interfaces.Repositories;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Application.Services;

/// <summary>
/// Handles user authentication, session creation, lockout policy, and password validation
/// (Req 3.1–3.8).
/// </summary>
public sealed class AuthenticationService
{
    private const int MaxFailedAttempts = 3;
    private static readonly TimeSpan FailedWindowDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan SessionDuration = TimeSpan.FromHours(8);
    private const int TokenSizeBytes = 16; // 128-bit

    private static readonly string SpecialChars = "!@#$%^&*()_+-=[]{}|;:,.<>?";

    /// <summary>
    /// Pre-computed dummy BCrypt hash (cost 12) for timing-safe comparison when username doesn't exist.
    /// This ensures the Verify call takes the same time whether or not the user exists.
    /// </summary>
    private const string DummyHash =
        "$2a$12$LJ3m4lM5bE7YPvi0bOKPwuhRAUvGHAa9lEI4G6Fs.z0r2huNKzCRO";

    private readonly IUserRepository _userRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _auditWriter;

    public AuthenticationService(
        IUserRepository userRepository,
        ISessionRepository sessionRepository,
        IPasswordHasher passwordHasher,
        IClock clock,
        IUnitOfWork unitOfWork,
        IAuditWriter auditWriter)
    {
        _userRepository = userRepository;
        _sessionRepository = sessionRepository;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _auditWriter = auditWriter;
    }

    /// <summary>
    /// Authenticates a user and creates a session on success.
    /// Returns identical "Invalid credentials" error for both wrong username and wrong password (Req 3.2).
    /// Implements timing-safe dummy verification for non-existent usernames.
    /// </summary>
    public async Task<Result<LoginResult>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var user = await _userRepository.GetByUsernameAsync(request.Username, ct);

        if (user is null)
        {
            // Perform dummy BCrypt verification to equalize timing (Req 3.2)
            _passwordHasher.Verify("dummy_input", DummyHash);

            await WriteFailedLoginAuditAsync(null, request.Username, request.IpAddress, ct);
            return Result<LoginResult>.Failure(ErrorCode.InvalidCredentials);
        }

        // Check if account is locked (Req 3.5, 3.8)
        if (user.LockedUntil.HasValue)
        {
            if (now < user.LockedUntil.Value)
            {
                // Still locked — perform dummy verify for timing equality
                _passwordHasher.Verify("dummy_input", DummyHash);
                await WriteFailedLoginAuditAsync(user.Id, user.Username, request.IpAddress, ct);
                return Result<LoginResult>.Failure(ErrorCode.AccountLocked);
            }

            // Lockout expired — auto-unlock (Req 3.8)
            user.LockedUntil = null;
            user.FailedLoginCount = 0;
            user.FailedWindowStartedAt = null;
        }

        // Verify password
        bool passwordValid = _passwordHasher.Verify(request.Password, user.PasswordHash);

        if (!passwordValid)
        {
            await HandleFailedAttemptAsync(user, now, request.IpAddress, ct);

            // Return appropriate error based on lockout state
            if (user.LockedUntil.HasValue)
                return Result<LoginResult>.Failure(ErrorCode.AccountLocked);

            return Result<LoginResult>.Failure(ErrorCode.InvalidCredentials);
        }

        // Successful login — reset failed count and create session
        user.FailedLoginCount = 0;
        user.FailedWindowStartedAt = null;

        var (rawToken, tokenHash) = GenerateSessionToken();
        var session = new Session
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now.Add(SessionDuration),
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent
        };

        await _sessionRepository.AddAsync(session, ct);

        // Enqueue audit entry for successful login
        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "Login",
            EntityType: "Session",
            EntityId: session.Id,
            RelatedEntityIds: null,
            BeforeState: null,
            AfterState: null,
            Metadata: $"{{\"user_id\":\"{user.Id}\",\"ip\":\"{request.IpAddress}\"}}"));

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }

        var result = new LoginResult(
            SessionId: session.Id,
            UserId: user.Id,
            Username: user.Username,
            RawToken: Convert.ToBase64String(rawToken),
            ExpiresAt: session.ExpiresAt);

        return Result<LoginResult>.Success(result);
    }

    /// <summary>
    /// Validates a password against the system requirements (Req 3.4):
    /// 8-128 characters, at least one uppercase, one lowercase, one digit, one special character.
    /// </summary>
    public static Result<Common.Unit> ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8 || password.Length > 128)
            return Result<Common.Unit>.Failure(ErrorCode.PasswordRequirementsNotMet);

        bool hasUpper = false, hasLower = false, hasDigit = false, hasSpecial = false;

        foreach (char c in password)
        {
            if (char.IsUpper(c)) hasUpper = true;
            else if (char.IsLower(c)) hasLower = true;
            else if (char.IsDigit(c)) hasDigit = true;
            else if (SpecialChars.Contains(c)) hasSpecial = true;
        }

        if (!hasUpper || !hasLower || !hasDigit || !hasSpecial)
            return Result<Common.Unit>.Failure(ErrorCode.PasswordRequirementsNotMet);

        return Result<Common.Unit>.Success(Common.Unit.Value);
    }

    /// <summary>
    /// Handles a failed login attempt: increments the counter, starts or resets the window,
    /// and locks the account if threshold is reached (Req 3.5).
    /// </summary>
    private async Task HandleFailedAttemptAsync(User user, DateTimeOffset now, string? ipAddress, CancellationToken ct)
    {
        // If no window or window has expired, start a new one
        if (!user.FailedWindowStartedAt.HasValue || now - user.FailedWindowStartedAt.Value > FailedWindowDuration)
        {
            user.FailedWindowStartedAt = now;
            user.FailedLoginCount = 1;
        }
        else
        {
            user.FailedLoginCount++;
        }

        // Lock the account if threshold reached (Req 3.5)
        if (user.FailedLoginCount >= MaxFailedAttempts)
        {
            user.LockedUntil = now.Add(LockoutDuration);
        }

        _userRepository.Update(user);

        await WriteFailedLoginAuditAsync(user.Id, user.Username, ipAddress, ct);

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// Generates a 128-bit cryptographically random session token and its SHA-256 hash.
    /// The raw token is returned to the caller; only the hash is persisted (Req 3.3).
    /// </summary>
    private static (byte[] RawToken, byte[] TokenHash) GenerateSessionToken()
    {
        byte[] rawToken = RandomNumberGenerator.GetBytes(TokenSizeBytes);
        byte[] tokenHash = SHA256.HashData(rawToken);
        return (rawToken, tokenHash);
    }

    private async Task WriteFailedLoginAuditAsync(Guid? userId, string username, string? ipAddress, CancellationToken ct)
    {
        var auditContext = new AuditContext(
            UserId: userId,
            UsernameSnapshot: username,
            SessionId: null,
            IpAddress: ipAddress,
            EntityType: "User",
            EntityId: userId,
            Metadata: null);

        await _auditWriter.WriteFailedAttemptAsync(ErrorCode.InvalidCredentials, auditContext, ct);
    }
}
