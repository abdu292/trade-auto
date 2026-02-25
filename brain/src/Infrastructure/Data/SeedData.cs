using Brain.Domain.Common;
using Brain.Domain.Entities;
using Brain.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Brain.Infrastructure.Data;

public static class SeedData
{
    public static readonly StrategyProfileId MomentumStrategyId = new(new Guid("11111111-1111-1111-1111-111111111111"));
    public static readonly StrategyProfileId MeanReversionStrategyId = new(new Guid("22222222-2222-2222-2222-222222222222"));
    public static readonly RiskProfileId BalancedRiskId = new(new Guid("33333333-3333-3333-3333-333333333333"));
    public static readonly RiskProfileId ConservativeRiskId = new(new Guid("44444444-4444-4444-4444-444444444444"));

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StrategyProfile>().HasData(
            new
            {
                Id = MomentumStrategyId,
                Name = "Momentum",
                Description = "Default momentum strategy profile.",
                IsActive = true
            },
            new
            {
                Id = MeanReversionStrategyId,
                Name = "Mean Reversion",
                Description = "Fallback mean reversion profile.",
                IsActive = false
            });

        modelBuilder.Entity<RiskProfile>().HasData(
            new
            {
                Id = BalancedRiskId,
                Name = "Balanced",
                Level = new RiskLevel("Medium"),
                MaxDrawdownPercent = 5m,
                IsActive = true
            },
            new
            {
                Id = ConservativeRiskId,
                Name = "Conservative",
                Level = new RiskLevel("Low"),
                MaxDrawdownPercent = 2m,
                IsActive = false
            });
    }
}
