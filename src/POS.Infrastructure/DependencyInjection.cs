using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Interfaces.Infrastructure;
using POS.Infrastructure.Email;
using POS.Infrastructure.Jobs;
using POS.Infrastructure.Receipts;
using POS.Infrastructure.Reports;
using Quartz;

namespace POS.Infrastructure;

/// <summary>
/// Registers Infrastructure layer services into the DI container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Receipt rendering
        services.AddScoped<IReceiptRenderer, QuestPdfReceiptRenderer>();

        // Thermal printer gateway via typed HttpClient
        services.AddHttpClient<IPrinterGateway, EscPosPrinterGateway>(client =>
        {
            client.BaseAddress = new Uri("http://localhost:9100");
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        // Email sender
        services.Configure<SmtpSettings>(configuration.GetSection(SmtpSettings.SectionName));
        services.AddScoped<IEmailSender, MailKitEmailSender>();

        // Quartz.NET scheduler with background jobs
        services.AddQuartz(q =>
        {
            q.AddPosJobs();
        });
        services.AddQuartzHostedService(opts =>
        {
            opts.WaitForJobsToComplete = true;
        });

        // Report renderers
        services.AddSingleton<QuestPdfReportRenderer>();
        services.AddSingleton<ExcelReportRenderer>();
        services.AddSingleton<IReportRendererFactory, ReportRendererFactory>();

        return services;
    }
}
