using Microsoft.Extensions.Logging;
using POS.Domain.Abstractions;
using POS.Infrastructure.Data;
using Quartz;

namespace POS.Infrastructure.Jobs;

/// <summary>
/// Quartz job that marks store credit vouchers as expired once past their expiration date.
/// Runs daily.
/// </summary>
[DisallowConcurrentExecution]
public sealed partial class ExpireVouchersJob : IJob
{
    private readonly PosDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<ExpireVouchersJob> _logger;

    public ExpireVouchersJob(PosDbContext db, IClock clock, ILogger<ExpireVouchersJob> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var now = _clock.UtcNow;

        var expiredVouchers = await _db.StoreCreditVouchers
            .Where(v => v.Status == "unused" && v.ExpiresAt <= now)
            .ToListAsync(context.CancellationToken);

        if (expiredVouchers.Count == 0)
            return;

        foreach (var voucher in expiredVouchers)
        {
            voucher.Status = "expired";
        }

        await _db.SaveChangesAsync(context.CancellationToken);

        LogExpiredVouchers(_logger, expiredVouchers.Count);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Expired {Count} store credit voucher(s)")]
    private static partial void LogExpiredVouchers(ILogger logger, int count);
}
