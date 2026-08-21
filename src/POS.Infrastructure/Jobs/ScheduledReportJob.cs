using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using POS.Application.Commands;
using POS.Application.Interfaces.Infrastructure;
using POS.Application.Interfaces.Services;
using POS.Domain.Abstractions;
using POS.Infrastructure.Data;
using Quartz;

namespace POS.Infrastructure.Jobs;

/// <summary>
/// Quartz job that processes due scheduled reports: generates report, emails attachment,
/// and handles failures with notification (Req 7.7, 7.8, 7.9).
/// Runs every hour to check for schedules that are due.
/// </summary>
[DisallowConcurrentExecution]
public sealed partial class ScheduledReportJob : IJob
{
    private const int MaxEmailRetries = 3;

    private readonly PosDbContext _db;
    private readonly IReportEngine _reportEngine;
    private readonly IEmailSender _emailSender;
    private readonly IClock _clock;
    private readonly ILogger<ScheduledReportJob> _logger;

    public ScheduledReportJob(
        PosDbContext db,
        IReportEngine reportEngine,
        IEmailSender emailSender,
        IClock clock,
        ILogger<ScheduledReportJob> logger)
    {
        _db = db;
        _reportEngine = reportEngine;
        _emailSender = emailSender;
        _clock = clock;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var now = _clock.UtcNow;

        var activeSchedules = await _db.ReportSchedules
            .Include(s => s.CreatedByUser)
            .Where(s => s.IsActive)
            .ToListAsync(ct);

        if (activeSchedules.Count == 0)
            return;

        foreach (var schedule in activeSchedules)
        {
            if (!IsDue(schedule, now))
                continue;

            await ProcessScheduleAsync(schedule, now, ct);
        }
    }

    private async Task ProcessScheduleAsync(
        Domain.Entities.ReportSchedule schedule,
        DateTimeOffset now,
        CancellationToken ct)
    {
        try
        {
            // Parse filter JSON to get report parameters
            var (dateFrom, dateTo, categoryIds, userIds, includeChildCategories) = ParseFilter(schedule.FilterJson, now);

            // Determine export format
            var exportFormat = schedule.ExportFormat.Equals("excel", StringComparison.OrdinalIgnoreCase)
                ? ReportExportFormat.Excel
                : ReportExportFormat.Pdf;

            var cmd = new GenerateReportCommand(
                DateFrom: dateFrom,
                DateTo: dateTo,
                CategoryIds: categoryIds,
                UserIds: userIds,
                IncludeChildCategories: includeChildCategories,
                ExportFormat: exportFormat,
                PerformedBy: schedule.CreatedBy);

            // Generate the report (Req 7.9)
            var result = await _reportEngine.GenerateAsync(cmd, ct);

            if (!result.IsSuccess)
            {
                LogReportGenerationFailed(_logger, schedule.Id, result.Error?.Code.ToString() ?? "Unknown");
                await MarkFailedAndNotifyAsync(schedule, now, "Report generation failed", ct);
                return;
            }

            // Parse recipients
            var recipients = JsonSerializer.Deserialize<List<string>>(schedule.Recipients) ?? new List<string>();
            if (recipients.Count == 0)
            {
                await MarkFailedAndNotifyAsync(schedule, now, "No recipients configured", ct);
                return;
            }

            // Send email with report attachment (Req 7.9)
            var attachment = new EmailAttachment(
                FileName: result.Value!.FileName,
                ContentType: result.Value.ContentType,
                Content: result.Value.Content);

            var subject = $"Scheduled Report: {schedule.ReportType} ({dateFrom:yyyy-MM-dd} to {dateTo:yyyy-MM-dd})";
            var body = $"Your scheduled {schedule.ReportType} report is attached.\n\n" +
                       $"Period: {dateFrom:yyyy-MM-dd} to {dateTo:yyyy-MM-dd}\n" +
                       $"Transactions: {result.Value.Summary.TransactionCount}\n" +
                       $"Total Sales: {result.Value.Summary.TotalSales:F2}";

            // IEmailSender already has 3 retries built in (Req 7.8)
            await _emailSender.SendAsync(subject, body, recipients, new[] { attachment }, ct);

            // Mark success
            schedule.LastRunAt = now;
            schedule.LastRunStatus = "success";
            await _db.SaveChangesAsync(ct);

            LogScheduleCompleted(_logger, schedule.Id);
        }
        catch (Exception ex)
        {
            LogScheduleError(_logger, ex, schedule.Id);
            await MarkFailedAndNotifyAsync(schedule, now, ex.Message, ct);
        }
    }

    /// <summary>
    /// Marks the schedule as failed and notifies the creator via email (Req 7.8).
    /// </summary>
    private async Task MarkFailedAndNotifyAsync(
        Domain.Entities.ReportSchedule schedule,
        DateTimeOffset now,
        string reason,
        CancellationToken ct)
    {
        schedule.LastRunAt = now;
        schedule.LastRunStatus = "failed";
        await _db.SaveChangesAsync(ct);

        // Notify the user who created the schedule (Req 7.8)
        if (schedule.CreatedByUser?.Email is not null)
        {
            try
            {
                var notificationSubject = $"Scheduled Report Failed: {schedule.ReportType}";
                var notificationBody = $"Your scheduled report '{schedule.ReportType}' ({schedule.Frequency}) " +
                                       $"failed to generate or deliver.\n\n" +
                                       $"Reason: {reason}\n\n" +
                                       $"Please check your schedule configuration or contact support.";

                await _emailSender.SendAsync(
                    notificationSubject,
                    notificationBody,
                    new[] { schedule.CreatedByUser.Email },
                    ct: ct);
            }
            catch (Exception ex)
            {
                // Log but don't throw — notification failure shouldn't crash the job
                LogNotificationFailed(_logger, ex, schedule.Id, schedule.CreatedByUser.Email);
            }
        }
    }

    /// <summary>
    /// Determines whether a schedule is due for execution based on its frequency and last run time.
    /// </summary>
    private static bool IsDue(Domain.Entities.ReportSchedule schedule, DateTimeOffset now)
    {
        if (schedule.LastRunAt is null)
            return true; // Never run before

        var lastRun = schedule.LastRunAt.Value;

        return schedule.Frequency.ToLowerInvariant() switch
        {
            "daily" => now >= lastRun.AddDays(1),
            "weekly" => now >= lastRun.AddDays(7),
            "monthly" => now >= lastRun.AddMonths(1),
            _ => false
        };
    }

    /// <summary>
    /// Parses the FilterJson into report parameters, defaulting to the appropriate period.
    /// </summary>
    private static (DateOnly DateFrom, DateOnly DateTo, IReadOnlyList<Guid>? CategoryIds, IReadOnlyList<Guid>? UserIds, bool IncludeChildCategories)
        ParseFilter(string filterJson, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        // Default period based on typical use: last 30 days
        var dateFrom = today.AddDays(-30);
        var dateTo = today.AddDays(-1);

        List<Guid>? categoryIds = null;
        List<Guid>? userIds = null;
        var includeChildCategories = true;

        if (!string.IsNullOrWhiteSpace(filterJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(filterJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("daysBack", out var daysBackProp) && daysBackProp.TryGetInt32(out var daysBack))
                {
                    dateFrom = today.AddDays(-daysBack);
                    dateTo = today.AddDays(-1);
                }

                if (root.TryGetProperty("dateFrom", out var dateFromProp))
                {
                    if (DateOnly.TryParse(dateFromProp.GetString(), out var parsed))
                        dateFrom = parsed;
                }

                if (root.TryGetProperty("dateTo", out var dateToProp))
                {
                    if (DateOnly.TryParse(dateToProp.GetString(), out var parsed))
                        dateTo = parsed;
                }

                if (root.TryGetProperty("categoryIds", out var catProp) && catProp.ValueKind == JsonValueKind.Array)
                {
                    categoryIds = new List<Guid>();
                    foreach (var item in catProp.EnumerateArray())
                    {
                        if (Guid.TryParse(item.GetString(), out var id))
                            categoryIds.Add(id);
                    }
                }

                if (root.TryGetProperty("userIds", out var userProp) && userProp.ValueKind == JsonValueKind.Array)
                {
                    userIds = new List<Guid>();
                    foreach (var item in userProp.EnumerateArray())
                    {
                        if (Guid.TryParse(item.GetString(), out var id))
                            userIds.Add(id);
                    }
                }

                if (root.TryGetProperty("includeChildCategories", out var inclProp))
                {
                    includeChildCategories = inclProp.GetBoolean();
                }
            }
            catch (JsonException)
            {
                // Use defaults on malformed JSON
            }
        }

        return (dateFrom, dateTo, categoryIds, userIds, includeChildCategories);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Report generation failed for schedule {ScheduleId}: {ErrorCode}")]
    private static partial void LogReportGenerationFailed(ILogger logger, Guid scheduleId, string errorCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scheduled report {ScheduleId} completed successfully")]
    private static partial void LogScheduleCompleted(ILogger logger, Guid scheduleId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Scheduled report {ScheduleId} failed with exception")]
    private static partial void LogScheduleError(ILogger logger, Exception ex, Guid scheduleId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to send failure notification for schedule {ScheduleId} to {Email}")]
    private static partial void LogNotificationFailed(ILogger logger, Exception ex, Guid scheduleId, string email);
}
