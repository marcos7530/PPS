using Microsoft.Extensions.Logging;
using POS.Domain.Abstractions;
using POS.Infrastructure.Data;
using Quartz;

namespace POS.Infrastructure.Jobs;

/// <summary>
/// Quartz job that deletes password reset tokens past their expiration date.
/// Runs daily to keep the table clean.
/// </summary>
[DisallowConcurrentExecution]
public sealed partial class PurgeExpiredResetTokensJob : IJob
{
    private readonly PosDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<PurgeExpiredResetTokensJob> _logger;

    public PurgeExpiredResetTokensJob(PosDbContext db, IClock clock, ILogger<PurgeExpiredResetTokensJob> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var now = _clock.UtcNow;

        var expiredTokens = await _db.PasswordResetTokens
            .Where(t => t.ExpiresAt <= now)
            .ToListAsync(context.CancellationToken);

        if (expiredTokens.Count == 0)
            return;

        _db.PasswordResetTokens.RemoveRange(expiredTokens);
        await _db.SaveChangesAsync(context.CancellationToken);

        LogPurgedTokens(_logger, expiredTokens.Count);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Purged {Count} expired password reset token(s)")]
    private static partial void LogPurgedTokens(ILogger logger, int count);
}
