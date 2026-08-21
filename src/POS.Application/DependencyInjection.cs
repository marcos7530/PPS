using Microsoft.Extensions.DependencyInjection;
using POS.Application.Interfaces.Services;
using POS.Application.Services;

namespace POS.Application;

/// <summary>
/// Registers Application layer services into the DI container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IReceiptService, ReceiptService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<IOperatingDayService, OperatingDayService>();
        services.AddScoped<IReportEngine, ReportEngine>();
        services.AddScoped<IScheduledReportService, ScheduledReportService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
