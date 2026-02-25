using Brain.Application.Common.Interfaces;
using Brain.Infrastructure.Data;
using Brain.Infrastructure.Services.Background;
using Brain.Infrastructure.Services.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Brain.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? configuration["ConnectionStrings__DefaultConnection"]
            ?? "Server=(localdb)\\mssqllocaldb;Database=TradeAutoDb;Trusted_Connection=True;MultipleActiveResultSets=true";

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<IAIWorkerClient, MockAIWorkerClient>();
        services.AddScoped<IMt5BridgeClient, MockMt5BridgeClient>();
        services.AddScoped<INotificationService, MockNotificationService>();
        services.AddScoped<IMarketDataProvider, MockMarketDataProvider>();
        services.AddScoped<IWhatsAppService, MockWhatsAppService>();
        services.AddScoped<ICalendarService, MockCalendarService>();

        services.AddHostedService<SessionSchedulerBackgroundService>();
        services.AddHostedService<SignalPollingBackgroundService>();

        return services;
    }
}
