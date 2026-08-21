using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace POS.Infrastructure.Jobs;

/// <summary>
/// Registers all Quartz.NET jobs with their appropriate triggers and schedules.
/// </summary>
public static class QuartzConfiguration
{
    public static IServiceCollectionQuartzConfigurator AddPosJobs(this IServiceCollectionQuartzConfigurator q)
    {
        // UnlockExpiredAccountsJob — every 5 minutes (Req 3.8)
        var unlockJobKey = new JobKey(nameof(UnlockExpiredAccountsJob));
        q.AddJob<UnlockExpiredAccountsJob>(opts => opts.WithIdentity(unlockJobKey));
        q.AddTrigger(opts => opts
            .ForJob(unlockJobKey)
            .WithIdentity($"{nameof(UnlockExpiredAccountsJob)}-trigger")
            .WithSimpleSchedule(s => s
                .WithIntervalInMinutes(5)
                .RepeatForever()));

        // ExpireVouchersJob — daily at 00:30 UTC
        var expireVouchersJobKey = new JobKey(nameof(ExpireVouchersJob));
        q.AddJob<ExpireVouchersJob>(opts => opts.WithIdentity(expireVouchersJobKey));
        q.AddTrigger(opts => opts
            .ForJob(expireVouchersJobKey)
            .WithIdentity($"{nameof(ExpireVouchersJob)}-trigger")
            .WithCronSchedule("0 30 0 * * ?"));

        // PurgeExpiredResetTokensJob — daily at 01:00 UTC
        var purgeTokensJobKey = new JobKey(nameof(PurgeExpiredResetTokensJob));
        q.AddJob<PurgeExpiredResetTokensJob>(opts => opts.WithIdentity(purgeTokensJobKey));
        q.AddTrigger(opts => opts
            .ForJob(purgeTokensJobKey)
            .WithIdentity($"{nameof(PurgeExpiredResetTokensJob)}-trigger")
            .WithCronSchedule("0 0 1 * * ?"));

        // RefreshDashboardAggregatesJob — every 15 minutes
        var refreshAggJobKey = new JobKey(nameof(RefreshDashboardAggregatesJob));
        q.AddJob<RefreshDashboardAggregatesJob>(opts => opts.WithIdentity(refreshAggJobKey));
        q.AddTrigger(opts => opts
            .ForJob(refreshAggJobKey)
            .WithIdentity($"{nameof(RefreshDashboardAggregatesJob)}-trigger")
            .WithSimpleSchedule(s => s
                .WithIntervalInMinutes(15)
                .RepeatForever()));

        // AuditPartitionMaintenanceJob — 1st of each month at 02:00 UTC
        var partitionJobKey = new JobKey(nameof(AuditPartitionMaintenanceJob));
        q.AddJob<AuditPartitionMaintenanceJob>(opts => opts.WithIdentity(partitionJobKey));
        q.AddTrigger(opts => opts
            .ForJob(partitionJobKey)
            .WithIdentity($"{nameof(AuditPartitionMaintenanceJob)}-trigger")
            .WithCronSchedule("0 0 2 1 * ?"));

        // ScheduledReportJob — every hour at minute 0 (Req 7.7)
        var scheduledReportJobKey = new JobKey(nameof(ScheduledReportJob));
        q.AddJob<ScheduledReportJob>(opts => opts.WithIdentity(scheduledReportJobKey));
        q.AddTrigger(opts => opts
            .ForJob(scheduledReportJobKey)
            .WithIdentity($"{nameof(ScheduledReportJob)}-trigger")
            .WithCronSchedule("0 0 * * * ?"));

        return q;
    }
}
