using System.Collections.Concurrent;
using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;

namespace Brain.Infrastructure.Services.External;

public sealed class InMemoryTradeLedgerService : ITradeLedgerService
{
    private const decimal OunceToGram = 31.1035m;
    private const decimal UsdToAed = 3.674m;
    private const decimal MinTradeGrams = 100m;
    private const decimal InitialCashAed = 100000m;

    private readonly Lock _gate = new();
    private readonly ConcurrentDictionary<Guid, PositionState> _openPositions = new();
    private readonly HashSet<Guid> _closedTrades = [];

    private decimal _cashAed = InitialCashAed;
    private decimal _goldGrams;

    public LedgerStateContract GetState()
    {
        lock (_gate)
        {
            var exposure = GetExposurePercentUnsafe();
            var deployable = decimal.Round(Math.Max(0m, _cashAed), 2);
            return new LedgerStateContract(
                CashAed: decimal.Round(_cashAed, 2),
                GoldGrams: decimal.Round(_goldGrams, 2),
                OpenExposurePercent: decimal.Round(exposure, 2),
                DeployableCashAed: deployable,
                OpenBuyCount: _openPositions.Count,
                BucketC1Aed: decimal.Round(deployable * 0.80m, 2),
                BucketC2Aed: decimal.Round(deployable * 0.20m, 2));
        }
    }

    public LedgerStateContract GetExtendedState(decimal currentBidPrice)
    {
        lock (_gate)
        {
            var exposure = GetExposurePercentUnsafe();
            var deployable = decimal.Round(Math.Max(0m, _cashAed), 2);
            var cashAed = decimal.Round(_cashAed, 2);
            var goldGrams = decimal.Round(_goldGrams, 2);
            var openExposurePercent = decimal.Round(exposure, 2);
            var bucketC1 = decimal.Round(deployable * 0.80m, 2);
            var bucketC2 = decimal.Round(deployable * 0.20m, 2);
            if (currentBidPrice <= 0m)
                return new LedgerStateContract(cashAed, goldGrams, openExposurePercent, deployable, _openPositions.Count, BucketC1Aed: bucketC1, BucketC2Aed: bucketC2);
            var shopSellBid = currentBidPrice - 0.80m;
            var goldAedEquivalent = decimal.Round(_goldGrams / OunceToGram * shopSellBid * UsdToAed, 2);
            var netEquityAed = decimal.Round(_cashAed + goldAedEquivalent, 2);
            var totalOpenCost = _openPositions.Values.Sum(x => x.DebitAed);
            return new LedgerStateContract(
                cashAed,
                goldGrams,
                openExposurePercent,
                deployable,
                _openPositions.Count,
                GoldAedEquivalent: goldAedEquivalent,
                NetEquityAed: netEquityAed,
                PurchasePowerAed: deployable,
                DeployedAed: decimal.Round(totalOpenCost, 2),
                OpenPositionsAed: decimal.Round(totalOpenCost, 2),
                PendingReservedAed: 0m,
                StartingInvestmentAed: 0m,
                EquityMultiple: 0m,
                BucketC1Aed: bucketC1,
                BucketC2Aed: bucketC2);
        }
    }

    public bool CanScaleIn(decimal currentPrice, RegimeClassificationContract regime, decimal minSpacingPercent, decimal exposureCapPercent)
    {
        lock (_gate)
        {
            if (regime.IsBlocked || regime.IsWaterfall || string.Equals(regime.RiskTag, "BLOCK", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (_cashAed <= 0)
            {
                return false;
            }

            var exposure = GetExposurePercentUnsafe();
            if (exposure >= exposureCapPercent)
            {
                return false;
            }

            var latestEntry = _openPositions.Values.OrderByDescending(x => x.Mt5BuyTime).FirstOrDefault();
            if (latestEntry is null)
            {
                return true;
            }

            var spacing = Math.Abs(currentPrice - latestEntry.Mt5BuyPrice) / latestEntry.Mt5BuyPrice;
            return spacing >= minSpacingPercent;
        }
    }

    public TradeSlipContract ApplyBuyFill(Guid tradeId, decimal grams, decimal mt5BuyPrice, DateTimeOffset mt5Time, string openedSession = "")
    {
        lock (_gate)
        {
            var normalizedGrams = Math.Max(0m, grams);
            if (normalizedGrams < MinTradeGrams)
            {
                throw new InvalidOperationException("Buy fill rejected: grams below 100g minimum.");
            }

            var shopBuy = mt5BuyPrice + 0.80m;
            var debit = ToAed(shopBuy, normalizedGrams);

            if (debit > _cashAed)
            {
                throw new InvalidOperationException("Buy fill rejected: insufficient ledger cash.");
            }

            _cashAed -= debit;
            _goldGrams += normalizedGrams;

            _openPositions[tradeId] = new PositionState(tradeId, normalizedGrams, mt5BuyPrice, shopBuy, debit, mt5Time);

            var ksaTime = mt5Time.AddMinutes(50);
            return new TradeSlipContract(
                SlipType: "BUY",
                TradeId: tradeId,
                Grams: decimal.Round(normalizedGrams, 2),
                Mt5Price: decimal.Round(mt5BuyPrice, 2),
                ShopPrice: decimal.Round(shopBuy, 2),
                AmountAed: decimal.Round(debit, 2),
                NetProfitAed: 0m,
                CashBalanceAed: decimal.Round(_cashAed, 2),
                GoldBalanceGrams: decimal.Round(_goldGrams, 2),
                Mt5Time: mt5Time,
                KsaTime: ksaTime,
                Message: $"BUY SLIP {tradeId} | {normalizedGrams:0.##}g @ {shopBuy:0.00} | Debit AED {debit:0.00}");
        }
    }

    public TradeSlipContract? ApplySellFill(Guid tradeId, decimal mt5SellPrice, DateTimeOffset mt5Time, string closedSession = "")
    {
        lock (_gate)
        {
            if (_closedTrades.Contains(tradeId))
            {
                return null;
            }

            if (!_openPositions.TryRemove(tradeId, out var position))
            {
                return null;
            }

            var shopSell = mt5SellPrice - 0.80m;
            var credit = ToAed(shopSell, position.Grams);
            var netProfit = credit - position.DebitAed;

            _cashAed += credit;
            _goldGrams -= position.Grams;
            if (_goldGrams < 0m)
            {
                _goldGrams = 0m;
            }

            _closedTrades.Add(tradeId);

            var ksaTime = mt5Time.AddMinutes(50);
            return new TradeSlipContract(
                SlipType: "SELL",
                TradeId: tradeId,
                Grams: decimal.Round(position.Grams, 2),
                Mt5Price: decimal.Round(mt5SellPrice, 2),
                ShopPrice: decimal.Round(shopSell, 2),
                AmountAed: decimal.Round(credit, 2),
                NetProfitAed: decimal.Round(netProfit, 2),
                CashBalanceAed: decimal.Round(_cashAed, 2),
                GoldBalanceGrams: decimal.Round(_goldGrams, 2),
                Mt5Time: mt5Time,
                KsaTime: ksaTime,
                Message: $"SELL SLIP {tradeId} | {position.Grams:0.##}g @ {shopSell:0.00} | Credit AED {credit:0.00} | Net AED {netProfit:0.00}");
        }
    }

    public TradeSlipContract AddCapital(decimal amountAed, string note, DateTimeOffset timestamp)
    {
        if (amountAed <= 0m)
        {
            throw new InvalidOperationException("Deposit amount must be positive.");
        }

        lock (_gate)
        {
            _cashAed += amountAed;
            return new TradeSlipContract(
                SlipType: "DEPOSIT",
                TradeId: Guid.NewGuid(),
                Grams: 0m,
                Mt5Price: 0m,
                ShopPrice: 0m,
                AmountAed: decimal.Round(amountAed, 2),
                NetProfitAed: 0m,
                CashBalanceAed: decimal.Round(_cashAed, 2),
                GoldBalanceGrams: decimal.Round(_goldGrams, 2),
                Mt5Time: timestamp,
                KsaTime: timestamp.AddHours(3),
                Message: $"DEPOSIT | AED {amountAed:0.00} | {note} | Balance AED {_cashAed:0.00}");
        }
    }

    public TradeSlipContract WithdrawCapital(decimal amountAed, string note, DateTimeOffset timestamp)
    {
        if (amountAed <= 0m)
        {
            throw new InvalidOperationException("Withdrawal amount must be positive.");
        }

        lock (_gate)
        {
            if (amountAed > _cashAed)
            {
                throw new InvalidOperationException("Withdrawal exceeds available cash balance.");
            }

            _cashAed -= amountAed;
            return new TradeSlipContract(
                SlipType: "WITHDRAWAL",
                TradeId: Guid.NewGuid(),
                Grams: 0m,
                Mt5Price: 0m,
                ShopPrice: 0m,
                AmountAed: decimal.Round(amountAed, 2),
                NetProfitAed: 0m,
                CashBalanceAed: decimal.Round(_cashAed, 2),
                GoldBalanceGrams: decimal.Round(_goldGrams, 2),
                Mt5Time: timestamp,
                KsaTime: timestamp.AddHours(3),
                Message: $"WITHDRAWAL | AED {amountAed:0.00} | {note} | Balance AED {_cashAed:0.00}");
        }
    }

    public TradeSlipContract ShopAdjustment(decimal adjustmentAed, string note, DateTimeOffset timestamp)
    {
        lock (_gate)
        {
            _cashAed += adjustmentAed;
            return new TradeSlipContract(
                SlipType: "ADJUSTMENT",
                TradeId: Guid.NewGuid(),
                Grams: 0m,
                Mt5Price: 0m,
                ShopPrice: 0m,
                AmountAed: decimal.Round(Math.Abs(adjustmentAed), 2),
                NetProfitAed: decimal.Round(adjustmentAed, 2),
                CashBalanceAed: decimal.Round(_cashAed, 2),
                GoldBalanceGrams: decimal.Round(_goldGrams, 2),
                Mt5Time: timestamp,
                KsaTime: timestamp.AddHours(3),
                Message: $"SHOP_ADJUSTMENT | AED {adjustmentAed:+0.00;-0.00} | {note} | Balance AED {_cashAed:0.00}");
        }
    }

    public void SyncRuntimeState(decimal cashAed, decimal goldGrams, DateTimeOffset timestamp)
    {
        lock (_gate)
        {
            _cashAed = Math.Max(0m, cashAed);
            _goldGrams = Math.Max(0m, goldGrams);
        }
    }

    private decimal GetExposurePercentUnsafe()
    {
        var totalOpenCost = _openPositions.Values.Sum(x => x.DebitAed);
        var equity = _cashAed + totalOpenCost;
        if (equity <= 0m)
        {
            return 100m;
        }

        return (totalOpenCost / equity) * 100m;
    }

    private static decimal ToAed(decimal usdPerOunce, decimal grams)
    {
        if (usdPerOunce <= 0m || grams <= 0m)
        {
            return 0m;
        }

        var usdPerGram = usdPerOunce / OunceToGram;
        var usd = usdPerGram * grams;
        return usd * UsdToAed;
    }

    private sealed record PositionState(
        Guid TradeId,
        decimal Grams,
        decimal Mt5BuyPrice,
        decimal ShopBuyPrice,
        decimal DebitAed,
        DateTimeOffset Mt5BuyTime);
}
