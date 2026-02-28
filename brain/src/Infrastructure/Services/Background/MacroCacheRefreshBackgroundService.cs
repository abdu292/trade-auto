using Brain.Application.Common.Interfaces;
using Brain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Brain.Infrastructure.Services.Background;

public sealed class MacroCacheRefreshBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<MacroCacheRefreshBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var snapshotStore = scope.ServiceProvider.GetRequiredService<ILatestMarketSnapshotStore>();
                var tvStore = scope.ServiceProvider.GetRequiredService<ITradingViewSignalStore>();

                if (!snapshotStore.TryGet(out var snapshot) || snapshot is null)
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    continue;
                }

                tvStore.TryGetLatest(out var tv);
                var macroBias = ResolveMacroBias(snapshot, tv);
                var institutionalBias = ResolveInstitutionalBias(snapshot, tv);
                var cbFlowFlag = "UNKNOWN";
                var positioningFlag = ResolvePositioningFlag(snapshot, tv);

                var cache = await db.MacroCacheStates.FirstOrDefaultAsync(stoppingToken);
                if (cache is null)
                {
                    cache = Domain.Entities.MacroCacheState.CreateDefault();
                    db.MacroCacheStates.Add(cache);
                }

                cache.Refresh(macroBias, institutionalBias, cbFlowFlag, positioningFlag, "HEURISTIC_ASYNC");
                await db.SaveChangesAsync(stoppingToken);

                logger.LogInformation(
                    "Macro cache refreshed: macro={MacroBias}, institutional={InstitutionalBias}, positioning={PositioningFlag}",
                    macroBias,
                    institutionalBias,
                    positioningFlag);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Macro cache refresh failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }

    private static string ResolveMacroBias(Application.Common.Models.MarketSnapshotContract snapshot, Application.Common.Models.TradingViewSignalContract? tv)
    {
        if (snapshot.IsFriday && snapshot.IsLondonNyOverlap)
        {
            return "HOSTILE";
        }

        if (tv is not null && tv.Bias == "BULLISH" && tv.RiskTag != "BLOCK")
        {
            return "SUPPORTIVE";
        }

        if (tv is not null && tv.Bias == "BEARISH")
        {
            return "HOSTILE";
        }

        return "NEUTRAL";
    }

    private static string ResolveInstitutionalBias(Application.Common.Models.MarketSnapshotContract snapshot, Application.Common.Models.TradingViewSignalContract? tv)
    {
        if (snapshot.RsiH1 > 72m || snapshot.PanicSuspected)
        {
            return "HOSTILE";
        }

        if (tv is not null && tv.ConfirmationTag == "CONFIRM" && tv.Bias == "BULLISH")
        {
            return "SUPPORTIVE";
        }

        return "NEUTRAL";
    }

    private static string ResolvePositioningFlag(Application.Common.Models.MarketSnapshotContract snapshot, Application.Common.Models.TradingViewSignalContract? tv)
    {
        if (snapshot.PanicSuspected || (tv is not null && tv.Bias == "BEARISH"))
        {
            return "RISK_OFF_GOLD";
        }

        if (tv is not null && tv.Bias == "BULLISH")
        {
            return "RISK_ON_GOLD";
        }

        return "UNKNOWN";
    }
}
