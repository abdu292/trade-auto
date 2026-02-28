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

        // HttpClient for AI Worker service at http://localhost:8001
        services.AddHttpClient<HttpAIWorkerClient>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));
        
        services.AddScoped<IAIWorkerClient>(provider => provider.GetRequiredService<HttpAIWorkerClient>());
        services.AddSingleton<ILatestMarketSnapshotStore, InMemoryLatestMarketSnapshotStore>();
        services.AddSingleton<IPendingTradeStore, InMemoryPendingTradeStore>();
        services.AddSingleton<ITradeApprovalStore, InMemoryTradeApprovalStore>();
        services.AddSingleton<ITradingViewSignalStore, InMemoryTradingViewSignalStore>();
        services.AddSingleton<INotificationFeedStore, InMemoryNotificationFeedStore>();
        services.AddSingleton<ITradeLedgerService, InMemoryTradeLedgerService>();
        services.AddSingleton<IMarketSimulationService, WeekendMarketSimulationService>();
        services.AddScoped<IMt5BridgeClient, MockMt5BridgeClient>();
        services.AddHttpClient<TelegramNotificationService>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        var telegramBotToken = (configuration["External:Telegram:BotToken"] ?? configuration["TELEGRAM_BOT_TOKEN"] ?? string.Empty).Trim();
        var telegramTargets = (
            configuration["External:Telegram:NotifyChannels"]
            ?? configuration["TELEGRAM_NOTIFY_CHANNELS"]
            ?? configuration["TELEGRAM_CHANNELS"]
            ?? string.Empty).Trim();

        if (!string.IsNullOrWhiteSpace(telegramBotToken) && !string.IsNullOrWhiteSpace(telegramTargets))
        {
            services.AddScoped<INotificationService, TelegramNotificationService>();
        }
        else
        {
            services.AddScoped<INotificationService, MockNotificationService>();
        }

        services.AddScoped<IMarketDataProvider, Mt5MarketDataProvider>();
        services.AddScoped<IWhatsAppService, MockWhatsAppService>();
        services.AddScoped<ICalendarService, MockCalendarService>();

        services.AddHostedService<SessionSchedulerBackgroundService>();
        services.AddHostedService<SignalPollingBackgroundService>();
        services.AddHostedService<MacroCacheRefreshBackgroundService>();

        return services;
    }
}
