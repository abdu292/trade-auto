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
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
