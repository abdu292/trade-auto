using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// SLIPS Service per spec/00_instructions
/// Position: after fills/exits
/// Role: create buy/sell slips, update ledger, preserve bullion truth
/// </summary>
public static class SlipsService
{
    private const decimal OunceToGram = 31.1035m;
    private const decimal UsdToAed = 3.674m;

    public record BuySlip(
        DateTimeOffset Timestamp,
        decimal PriceUsd,
        decimal Grams,
        decimal TotalUsd,
        decimal TotalAed,
        string OrderId,
        string ReasonCode,
        string? Notes = null);

    public record SellSlip(
        DateTimeOffset Timestamp,
        decimal PriceUsd,
        decimal Grams,
        decimal TotalUsd,
        decimal TotalAed,
        decimal ProfitLossUsd,
        decimal ProfitLossAed,
        string OrderId,
        string ReasonCode,
        string? Notes = null);

    public record LedgerUpdate(
        decimal CashAedBefore,
        decimal CashAedAfter,
        decimal GoldGramsBefore,
        decimal GoldGramsAfter,
        decimal DeployableAedBefore,
        decimal DeployableAedAfter);

    /// <summary>
    /// Creates a buy slip and updates ledger.
    /// </summary>
    public static (BuySlip Slip, LedgerUpdate LedgerUpdate) CreateBuySlip(
        DateTimeOffset timestamp,
        decimal priceUsd,
        decimal grams,
        string orderId,
        string reasonCode,
        LedgerStateContract currentLedger,
        string? notes = null)
    {
        var totalUsd = priceUsd * grams;
        var totalAed = totalUsd * UsdToAed;

        // Update ledger
        var cashAedAfter = currentLedger.CashAedTotal - totalAed;
        var goldGramsAfter = currentLedger.GoldGramsTotal + grams;
        var deployableAedAfter = CalculateDeployableAed(cashAedAfter, goldGramsAfter, priceUsd);

        var slip = new BuySlip(
            timestamp,
            priceUsd,
            grams,
            totalUsd,
            totalAed,
            orderId,
            reasonCode,
            notes);

        var ledgerUpdate = new LedgerUpdate(
            currentLedger.CashAedTotal,
            cashAedAfter,
            currentLedger.GoldGramsTotal,
            goldGramsAfter,
            currentLedger.DeployableAed,
            deployableAedAfter);

        return (slip, ledgerUpdate);
    }

    /// <summary>
    /// Creates a sell slip and updates ledger.
    /// </summary>
    public static (SellSlip Slip, LedgerUpdate LedgerUpdate) CreateSellSlip(
        DateTimeOffset timestamp,
        decimal priceUsd,
        decimal grams,
        decimal averageBuyPriceUsd,  // Average cost basis
        string orderId,
        string reasonCode,
        LedgerStateContract currentLedger,
        string? notes = null)
    {
        var totalUsd = priceUsd * grams;
        var totalAed = totalUsd * UsdToAed;

        // Calculate profit/loss
        var costBasisUsd = averageBuyPriceUsd * grams;
        var profitLossUsd = totalUsd - costBasisUsd;
        var profitLossAed = profitLossUsd * UsdToAed;

        // Update ledger
        var cashAedAfter = currentLedger.CashAedTotal + totalAed;
        var goldGramsAfter = currentLedger.GoldGramsTotal - grams;
        var deployableAedAfter = CalculateDeployableAed(cashAedAfter, goldGramsAfter, priceUsd);

        var slip = new SellSlip(
            timestamp,
            priceUsd,
            grams,
            totalUsd,
            totalAed,
            profitLossUsd,
            profitLossAed,
            orderId,
            reasonCode,
            notes);

        var ledgerUpdate = new LedgerUpdate(
            currentLedger.CashAedTotal,
            cashAedAfter,
            currentLedger.GoldGramsTotal,
            goldGramsAfter,
            currentLedger.DeployableAed,
            deployableAedAfter);

        return (slip, ledgerUpdate);
    }

    /// <summary>
    /// Calculates deployable AED based on cash and gold holdings.
    /// </summary>
    private static decimal CalculateDeployableAed(
        decimal cashAed,
        decimal goldGrams,
        decimal currentGoldPriceUsd)
    {
        // Deployable = cash + (gold value in AED) - safety buffer
        var goldValueUsd = goldGrams * currentGoldPriceUsd;
        var goldValueAed = goldValueUsd * UsdToAed;
        var totalValueAed = cashAed + goldValueAed;

        // Safety buffer: keep 10% as reserve
        var deployableAed = totalValueAed * 0.9m;

        return Math.Max(0, deployableAed);
    }

    /// <summary>
    /// Calculates average buy price for FIFO/LIFO cost basis.
    /// Simple implementation: uses weighted average.
    /// </summary>
    public static decimal CalculateAverageBuyPrice(
        IReadOnlyCollection<BuySlip> buySlips)
    {
        if (buySlips == null || !buySlips.Any())
        {
            return 0m;
        }

        var totalGrams = buySlips.Sum(s => s.Grams);
        if (totalGrams == 0)
        {
            return 0m;
        }

        var weightedPrice = buySlips.Sum(s => s.PriceUsd * s.Grams) / totalGrams;
        return weightedPrice;
    }
}