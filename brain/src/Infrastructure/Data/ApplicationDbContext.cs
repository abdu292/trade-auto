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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        SeedData.Apply(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }
}
