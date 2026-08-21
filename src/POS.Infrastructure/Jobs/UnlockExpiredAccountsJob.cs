using Microsoft.Extensions.Logging;
using POS.Domain.Abstractions;
using POS.Infrastructure.Data;
using Quartz;

namespace POS.Infrastructure.Jobs;

/// <summary>
/// Quartz job that auto-unlocks user accounts whose lockout period has expired (Req 3.8).
/// Runs every 5 minutes.
/// </summary>
[DisallowConcurrentExecution]
public sealed partial class UnlockExpiredAccountsJob : IJob
{
    private readonly PosDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<UnlockExpiredAccountsJob> _logger;

    public UnlockExpiredAccountsJob(PosDbContext db, IClock clock, ILogger<UnlockExpiredAccountsJob> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var now = _clock.UtcNow;

        var lockedUsers = await _db.Users
            .Where(u => u.LockedUntil != null && u.LockedUntil <= now)
            .ToListAsync(context.CancellationToken);

        if (lockedUsers.Count == 0)
            return;

        foreach (var user in lockedUsers)
        {
            user.LockedUntil = null;
            user.FailedLoginCount = 0;
            user.FailedWindowStartedAt = null;
        }

        await _db.SaveChangesAsync(context.CancellationToken);

        LogUnlockedAccounts(_logger, lockedUsers.Count);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Unlocked {Count} expired account(s)")]
    private static partial void LogUnlockedAccounts(ILogger logger, int count);
}
