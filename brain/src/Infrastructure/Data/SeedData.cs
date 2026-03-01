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
    public static readonly Guid MacroCacheId = new("55555555-5555-5555-5555-555555555555");
    public static readonly Guid TelegramChannelDefault1 = new("66666666-6666-6666-6666-666666666661");
    public static readonly Guid TelegramChannelDefault2 = new("66666666-6666-6666-6666-666666666662");
    private static readonly DateTimeOffset SeedTimeUtc = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StrategyProfile>().HasData(
            new
            {
                Id = MomentumStrategyId,
                Name = "Standard",
                Description = "Baseline production strategy profile.",
                IsActive = true
            },
            new
            {
                Id = MeanReversionStrategyId,
                Name = "WarPremium",
                Description = "War expansion harvest profile with stricter kill-switch behavior.",
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

        modelBuilder.Entity<MacroCacheState>().HasData(
            new
            {
                Id = MacroCacheId,
                MacroBias = "UNKNOWN",
                InstitutionalBias = "UNKNOWN",
                CbFlowFlag = "UNKNOWN",
                PositioningFlag = "UNKNOWN",
                Source = "SEED",
                LastRefreshedUtc = SeedTimeUtc,
            });

        modelBuilder.Entity<TelegramChannel>().HasData(
            new
            {
                Id = TelegramChannelDefault1,
                ChannelKey = "@core_gold_1",
                Name = "Core Gold 1",
                Type = "NEWS",
                ReliabilityFlags = "unknown",
                Weight = 1.50m,
                WinRateRolling = 0m,
                ImpactScore = 0m,
                ConflictScore = 0m,
                LastActiveTimeUtc = SeedTimeUtc,
            },
            new
            {
                Id = TelegramChannelDefault2,
                ChannelKey = "@core_gold_2",
                Name = "Core Gold 2",
                Type = "INTRADAY",
                ReliabilityFlags = "unknown",
                Weight = 1.25m,
                WinRateRolling = 0m,
                ImpactScore = 0m,
                ConflictScore = 0m,
                LastActiveTimeUtc = SeedTimeUtc,
            });
    }
}
