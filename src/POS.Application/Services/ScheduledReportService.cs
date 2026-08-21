using System.Text.Json;
using POS.Application.Commands;
using POS.Application.DTOs;
using POS.Application.Interfaces.Audit;
using POS.Application.Interfaces.Repositories;
using POS.Application.Interfaces.Services;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Application.Services;

/// <summary>
/// Manages CRUD operations for scheduled report configurations with validation (Req 7.7).
/// </summary>
public sealed class ScheduledReportService : IScheduledReportService
{
    private static readonly HashSet<string> ValidFrequencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "daily", "weekly", "monthly"
    };

    private static readonly HashSet<string> ValidFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "pdf", "excel"
    };

    private static readonly HashSet<string> ValidReportTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "sales", "inventory", "audit", "discounts", "margins"
    };

    private const int MaxRecipients = 10;

    private readonly IReportScheduleRepository _scheduleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditWriter _auditWriter;

    public ScheduledReportService(
        IReportScheduleRepository scheduleRepository,
        IUnitOfWork unitOfWork,
        IAuditWriter auditWriter)
    {
        _scheduleRepository = scheduleRepository;
        _unitOfWork = unitOfWork;
        _auditWriter = auditWriter;
    }

    public async Task<Result<ReportSchedule>> CreateScheduleAsync(CreateReportScheduleCommand cmd, CancellationToken ct = default)
    {
        // Validate frequency
        if (!ValidFrequencies.Contains(cmd.Frequency))
            return Result<ReportSchedule>.Failure(ErrorCode.UnexpectedError);

        // Validate export format
        if (!ValidFormats.Contains(cmd.ExportFormat))
            return Result<ReportSchedule>.Failure(ErrorCode.UnexpectedError);

        // Validate report type
        if (!ValidReportTypes.Contains(cmd.ReportType))
            return Result<ReportSchedule>.Failure(ErrorCode.UnexpectedError);

        // Validate recipients (1-10 emails, Req 7.7)
        if (cmd.Recipients.Count == 0 || cmd.Recipients.Count > MaxRecipients)
            return Result<ReportSchedule>.Failure(ErrorCode.UnexpectedError);

        foreach (var email in cmd.Recipients)
        {
            if (!IsValidEmail(email))
                return Result<ReportSchedule>.Failure(ErrorCode.InvalidEmailFormat);
        }

        var schedule = new ReportSchedule
        {
            Id = Guid.NewGuid(),
            CreatedBy = cmd.CreatedBy,
            ReportType = cmd.ReportType.ToLowerInvariant(),
            Frequency = cmd.Frequency.ToLowerInvariant(),
            ExportFormat = cmd.ExportFormat.ToLowerInvariant(),
            Recipients = JsonSerializer.Serialize(cmd.Recipients),
            FilterJson = cmd.FilterJson,
            IsActive = true,
            LastRunAt = null,
            LastRunStatus = null
        };

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            _auditWriter.Enqueue(new AuditEntryDraft(
                OperationType: "CreateReportSchedule",
                EntityType: "ReportSchedule",
                EntityId: schedule.Id,
                RelatedEntityIds: null,
                BeforeState: null,
                AfterState: JsonSerializer.Serialize(schedule),
                Metadata: null));

            await _scheduleRepository.AddAsync(schedule, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            return Result<ReportSchedule>.Success(schedule);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            return Result<ReportSchedule>.Failure(ErrorCode.UnexpectedError);
        }
    }

    public async Task<Result<ReportSchedule>> UpdateScheduleAsync(UpdateReportScheduleCommand cmd, CancellationToken ct = default)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(cmd.ScheduleId, ct);
        if (schedule is null)
            return Result<ReportSchedule>.Failure(ErrorCode.UnexpectedError);

        var beforeState = JsonSerializer.Serialize(schedule);

        if (cmd.Frequency is not null)
        {
            if (!ValidFrequencies.Contains(cmd.Frequency))
                return Result<ReportSchedule>.Failure(ErrorCode.UnexpectedError);
            schedule.Frequency = cmd.Frequency.ToLowerInvariant();
        }

        if (cmd.ExportFormat is not null)
        {
            if (!ValidFormats.Contains(cmd.ExportFormat))
                return Result<ReportSchedule>.Failure(ErrorCode.UnexpectedError);
            schedule.ExportFormat = cmd.ExportFormat.ToLowerInvariant();
        }

        if (cmd.Recipients is not null)
        {
            if (cmd.Recipients.Count == 0 || cmd.Recipients.Count > MaxRecipients)
                return Result<ReportSchedule>.Failure(ErrorCode.UnexpectedError);

            foreach (var email in cmd.Recipients)
            {
                if (!IsValidEmail(email))
                    return Result<ReportSchedule>.Failure(ErrorCode.InvalidEmailFormat);
            }

            schedule.Recipients = JsonSerializer.Serialize(cmd.Recipients);
        }

        if (cmd.FilterJson is not null)
            schedule.FilterJson = cmd.FilterJson;

        if (cmd.IsActive.HasValue)
            schedule.IsActive = cmd.IsActive.Value;

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            _auditWriter.Enqueue(new AuditEntryDraft(
                OperationType: "UpdateReportSchedule",
                EntityType: "ReportSchedule",
                EntityId: schedule.Id,
                RelatedEntityIds: null,
                BeforeState: beforeState,
                AfterState: JsonSerializer.Serialize(schedule),
                Metadata: null));

            _scheduleRepository.Update(schedule);
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            return Result<ReportSchedule>.Success(schedule);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            return Result<ReportSchedule>.Failure(ErrorCode.UnexpectedError);
        }
    }

    public async Task<Result<bool>> DeleteScheduleAsync(Guid scheduleId, Guid performedBy, CancellationToken ct = default)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(scheduleId, ct);
        if (schedule is null)
            return Result<bool>.Failure(ErrorCode.UnexpectedError);

        var beforeState = JsonSerializer.Serialize(schedule);
        schedule.IsActive = false;

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            _auditWriter.Enqueue(new AuditEntryDraft(
                OperationType: "DeactivateReportSchedule",
                EntityType: "ReportSchedule",
                EntityId: schedule.Id,
                RelatedEntityIds: null,
                BeforeState: beforeState,
                AfterState: JsonSerializer.Serialize(schedule),
                Metadata: null));

            _scheduleRepository.Update(schedule);
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            return Result<bool>.Success(true);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            return Result<bool>.Failure(ErrorCode.UnexpectedError);
        }
    }

    public async Task<IReadOnlyList<ReportSchedule>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await _scheduleRepository.GetByCreatorAsync(userId, ct);
    }

    public async Task<ReportSchedule?> GetByIdAsync(Guid scheduleId, CancellationToken ct = default)
    {
        return await _scheduleRepository.GetByIdAsync(scheduleId, ct);
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 100)
            return false;

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0 || atIndex >= email.Length - 1)
            return false;

        var dotIndex = email.LastIndexOf('.');
        return dotIndex > atIndex + 1 && dotIndex < email.Length - 1;
    }
}
