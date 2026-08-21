using POS.Domain.Entities;

namespace POS.Application.Interfaces.Repositories;

public interface IReportScheduleRepository : IRepository<ReportSchedule>
{
    Task<IReadOnlyList<ReportSchedule>> GetActiveAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReportSchedule>> GetByCreatorAsync(Guid userId, CancellationToken ct = default);
}
