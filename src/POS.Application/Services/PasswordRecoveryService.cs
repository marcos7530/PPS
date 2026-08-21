using System.Security.Cryptography;
using POS.Application.Common;
using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Infrastructure;
using POS.Application.Interfaces.Repositories;
using POS.Domain.Abstractions;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Application.Services;

/// <summary>
/// Handles password recovery: token generation, email delivery with retries,
/// and password reset with session invalidation (Req 4.1–4.10).
/// </summary>
public sealed class PasswordRecoveryService
{
    private const int TokenSizeBytes = 16; // 128-bit
    private static readonly TimeSpan TokenExpiration = TimeSpan.FromHours(24);
    private const int MaxEmailRetries = 3;
    private const int MaxRequestsPerHour = 5;
    private const string ResetBaseUrl = "/reset-password?token=";

    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IEmailSender _emailSender;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _auditWriter;

    public PasswordRecoveryService(
        IUserRepository userRepository,
        IPasswordResetTokenRepository tokenRepository,
        ISessionRepository sessionRepository,
        IEmailSender emailSender,
        IPasswordHasher passwordHasher,
        IClock clock,
        IUnitOfWork unitOfWork,
        IAuditWriter auditWriter)
    {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _sessionRepository = sessionRepository;
        _emailSender = emailSender;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _auditWriter = auditWriter;
    }

    /// <summary>
    /// Requests a password reset. Returns the same success response regardless of whether
    /// the email exists in the system (Req 4.2).
    /// </summary>
    public async Task<Result<Unit>> RequestResetAsync(string email, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var user = await _userRepository.GetByEmailAsync(email, ct);

        // For non-existent email, return success without revealing non-existence (Req 4.2)
        if (user is null)
            return Result<Unit>.Success(Unit.Value);

        // Rate limit: max 5 requests per email per hour (Req 4.10)
        var since = now.AddHours(-1);
        var recentCount = await _tokenRepository.CountRecentByUserAsync(user.Id, since, ct);
        if (recentCount >= MaxRequestsPerHour)
            return Result<Unit>.Success(Unit.Value); // Same response, don't reveal rate limiting

        // Invalidate all existing tokens for this user (Req 4.8)
        await _tokenRepository.InvalidateAllForUserAsync(user.Id, ct);

        // Generate 128-bit cryptographically random token (Req 4.1)
        var rawToken = RandomNumberGenerator.GetBytes(TokenSizeBytes);
        var tokenHash = SHA256.HashData(rawToken);

        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = now.Add(TokenExpiration)
        };

        await _tokenRepository.AddAsync(resetToken, ct);

        // Enqueue audit entry for token generation
        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "PasswordResetRequested",
            EntityType: "PasswordResetToken",
            EntityId: resetToken.Id,
            RelatedEntityIds: null,
            BeforeState: null,
            AfterState: null,
            Metadata: $"{{\"user_id\":\"{user.Id}\"}}"));

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

        // Send reset email with 3 retry attempts (Req 4.3, 4.4)
        var resetUrl = $"{ResetBaseUrl}{Convert.ToBase64String(rawToken)}";
        var emailSent = await SendResetEmailWithRetriesAsync(user.Email, resetUrl, ct);

        if (!emailSent)
            return Result<Unit>.Failure(ErrorCode.ResetEmailSendFailed);

        return Result<Unit>.Success(Unit.Value);
    }

    /// <summary>
    /// Resets a user's password using a valid token.
    /// Invalidates all active sessions on success (Req 4.9).
    /// </summary>
    public async Task<Result<Unit>> ResetPasswordAsync(string rawTokenBase64, string newPassword, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        // Decode token and compute hash
        byte[] rawToken;
        try
        {
            rawToken = Convert.FromBase64String(rawTokenBase64);
        }
        catch (FormatException)
        {
            return Result<Unit>.Failure(ErrorCode.InvalidOrExpiredResetToken);
        }

        var tokenHash = SHA256.HashData(rawToken);

        // Find active token (Req 4.6)
        var resetToken = await _tokenRepository.GetActiveByTokenHashAsync(tokenHash, ct);

        if (resetToken is null || !resetToken.IsUsable(now))
            return Result<Unit>.Failure(ErrorCode.InvalidOrExpiredResetToken);

        // Validate new password (Req 4.7)
        var passwordValidation = AuthenticationService.ValidatePassword(newPassword);
        if (!passwordValidation.IsSuccess)
            return Result<Unit>.Failure(ErrorCode.PasswordRequirementsNotMet);

        // Load user to update password hash
        var user = await _userRepository.GetByIdAsync(resetToken.UserId, ct);
        if (user is null)
            return Result<Unit>.Failure(ErrorCode.InvalidOrExpiredResetToken);

        // Update password hash (Req 4.5)
        user.PasswordHash = _passwordHasher.Hash(newPassword);
        user.UpdatedAt = now;
        _userRepository.Update(user);

        // Mark token as consumed (Req 4.5)
        resetToken.ConsumedAt = now;
        _tokenRepository.Update(resetToken);

        // Invalidate all active sessions (Req 4.9)
        await _sessionRepository.RevokeAllForUserAsync(user.Id, ct);

        // Enqueue audit entry for password change
        _auditWriter.Enqueue(new AuditEntryDraft(
            OperationType: "PasswordReset",
            EntityType: "User",
            EntityId: user.Id,
            RelatedEntityIds: null,
            BeforeState: null,
            AfterState: null,
            Metadata: $"{{\"token_id\":\"{resetToken.Id}\"}}"));

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

        return Result<Unit>.Success(Unit.Value);
    }

    /// <summary>
    /// Sends the reset email with up to 3 retry attempts.
    /// Returns true if email was sent successfully, false after all retries exhausted.
    /// </summary>
    private async Task<bool> SendResetEmailWithRetriesAsync(string recipientEmail, string resetUrl, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= MaxEmailRetries; attempt++)
        {
            try
            {
                await _emailSender.SendAsync(
                    subject: "Password Reset Request",
                    body: $"Click the following link to reset your password: {resetUrl}",
                    recipients: new[] { recipientEmail },
                    attachments: null,
                    ct: ct);
                return true;
            }
            catch
            {
                if (attempt == MaxEmailRetries)
                    return false;
            }
        }

        return false;
    }
}
