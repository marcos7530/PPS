using POS.Application.Commands;
using POS.Domain.Common;
using POS.Domain.Entities;

namespace POS.Application.Interfaces.Services;

/// <summary>
/// Service port for managing scheduled report configurations (Req 7.7).
/// </summary>
public interface IScheduledReportService
{
    /// <summary>
    /// Creates a new report schedule with validation.
    /// </summary>
    Task<Result<ReportSchedule>> CreateScheduleAsync(CreateReportScheduleCommand cmd, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing report schedule.
    /// </summary>
    Task<Result<ReportSchedule>> UpdateScheduleAsync(UpdateReportScheduleCommand cmd, CancellationToken ct = default);

    /// <summary>
    /// Deletes (deactivates) a report schedule.
    /// </summary>
    Task<Result<bool>> DeleteScheduleAsync(Guid scheduleId, Guid performedBy, CancellationToken ct = default);

    /// <summary>
    /// Gets all schedules created by a specific user.
    /// </summary>
    Task<IReadOnlyList<ReportSchedule>> GetByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets a schedule by its identifier.
    /// </summary>
    Task<ReportSchedule?> GetByIdAsync(Guid scheduleId, CancellationToken ct = default);
}
