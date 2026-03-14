using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Services;
using Brain.Infrastructure.Data;
using Brain.Infrastructure.Services;
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

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null)));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // HttpClient for AI Worker service at http://localhost:8001
        var aiWorkerTimeoutSeconds = configuration.GetValue<int?>("External:AIWorkerTimeoutSeconds") ?? 240;
        if (aiWorkerTimeoutSeconds <= 0)
        {
            aiWorkerTimeoutSeconds = 240;
        }

        services.AddHttpClient<HttpAIWorkerClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(aiWorkerTimeoutSeconds);
            })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));
        
        services.AddScoped<IAIWorkerClient>(provider => provider.GetRequiredService<HttpAIWorkerClient>());
        services.AddSingleton<ILatestMarketSnapshotStore, InMemoryLatestMarketSnapshotStore>();
        services.AddSingleton<IReplayLiveBridge, ReplayLiveBridge>();
        services.AddSingleton<IChartDataStore, InMemoryChartDataStore>();
        services.AddSingleton<IPendingTradeStore, InMemoryPendingTradeStore>();
        services.AddSingleton<IExpectedEntryStore, InMemoryExpectedEntryStore>();
        services.AddSingleton<IMt5ControlStore, InMemoryMt5ControlStore>();
        services.AddSingleton<IHistoryFetchStore, InMemoryHistoryFetchStore>();
        services.AddSingleton<ITradeApprovalStore, InMemoryTradeApprovalStore>();
        services.AddSingleton<ITradingViewSignalStore, InMemoryTradingViewSignalStore>();
        services.AddSingleton<ITelegramSignalStore, InMemoryTelegramSignalStore>();
        services.AddSingleton<IHistoricalPatternStore, InMemoryHistoricalPatternStore>();
        services.AddSingleton<INotificationFeedStore, InMemoryNotificationFeedStore>();
        services.AddSingleton<ITradingRuntimeSettingsStore>(_ =>
            new InMemoryTradingRuntimeSettingsStore(configuration["Execution:Symbol"] ?? "XAUUSD.gram"));
        services.AddSingleton<ILastGoldEngineStateStore, InMemoryLastGoldEngineStateStore>();
        services.AddSingleton<IPathProjectionStore, InMemoryPathProjectionStore>();
        services.AddSingleton<ISetupLifecycleStore, InMemorySetupLifecycleStore>();
        services.AddSingleton<IGoldEngineThresholds, GoldEngineThresholds>();
        services.AddSingleton<ITradeLedgerService, DurableTradeLedgerService>();
        services.AddScoped<IMt5BridgeClient, MockMt5BridgeClient>();
        services.AddHttpClient<TelegramNotificationService>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        var telegramBotToken = (configuration["External:Telegram:BotToken"] ?? configuration["TELEGRAM_BOT_TOKEN"] ?? string.Empty).Trim();
        var telegramTargets = (
            configuration["External:Telegram:NotifyChannels"]
            ?? configuration["TELEGRAM_NOTIFY_CHANNELS"]
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
        services.AddHttpClient<ForexFactoryNewsService>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));
        services.AddSingleton<IEconomicNewsService, ForexFactoryNewsService>();
        services.AddScoped<IRuntimeTimelineWriter, RuntimeTimelineWriter>();
        services.AddScoped<IWhatsAppService, MockWhatsAppService>();
        services.AddScoped<ICalendarService, MockCalendarService>();
        services.AddSingleton<IHistoricalReplayService, HistoricalReplayService>();

        // Dynamic Session Risk Service (singleton — tracks session modifiers across cycles)
        services.AddSingleton<DynamicSessionRiskService>();

        services.AddHostedService<SessionSchedulerBackgroundService>();
        services.AddHostedService<SignalPollingBackgroundService>();
        services.AddHostedService<MacroCacheRefreshBackgroundService>();

        return services;
    }
}
