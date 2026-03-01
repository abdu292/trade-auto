using Brain.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Brain.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Trade> Trades { get; }
    DbSet<TradeSignal> TradeSignals { get; }
    DbSet<StrategyProfile> StrategyProfiles { get; }
    DbSet<RiskProfile> RiskProfiles { get; }
    DbSet<SessionState> SessionStates { get; }
    DbSet<MarketSnapshot> MarketSnapshots { get; }
    DbSet<MacroCacheState> MacroCacheStates { get; }
    DbSet<HazardWindow> HazardWindows { get; }
    DbSet<DecisionLog> DecisionLogs { get; }
    DbSet<LedgerAccount> LedgerAccounts { get; }
    DbSet<LedgerPosition> LedgerPositions { get; }
    DbSet<TelegramChannel> TelegramChannels { get; }
    DbSet<TelegramSignal> TelegramSignals { get; }
    DbSet<TradingViewAlertLog> TradingViewAlertLogs { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
