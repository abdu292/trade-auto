using Brain.Application.Common.Interfaces;
using Brain.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Brain.Infrastructure.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<TradeSignal> TradeSignals => Set<TradeSignal>();
    public DbSet<StrategyProfile> StrategyProfiles => Set<StrategyProfile>();
    public DbSet<RiskProfile> RiskProfiles => Set<RiskProfile>();
    public DbSet<SessionState> SessionStates => Set<SessionState>();
    public DbSet<MarketSnapshot> MarketSnapshots => Set<MarketSnapshot>();
    public DbSet<MacroCacheState> MacroCacheStates => Set<MacroCacheState>();
    public DbSet<HazardWindow> HazardWindows => Set<HazardWindow>();
    public DbSet<DecisionLog> DecisionLogs => Set<DecisionLog>();
    public DbSet<TelegramChannel> TelegramChannels => Set<TelegramChannel>();
    public DbSet<TelegramSignal> TelegramSignals => Set<TelegramSignal>();
    public DbSet<TradingViewAlertLog> TradingViewAlertLogs => Set<TradingViewAlertLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        SeedData.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
