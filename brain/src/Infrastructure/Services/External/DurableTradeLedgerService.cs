using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;
using Brain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brain.Infrastructure.Services.External;

public sealed class DurableTradeLedgerService(IServiceScopeFactory scopeFactory) : ITradeLedgerService
{
    private const decimal OunceToGram = 31.1035m;
    private const decimal UsdToAed = 3.674m;
    private const decimal MinTradeGrams = 100m;

    private readonly Lock _gate = new();

    public LedgerStateContract GetState()
    {
        lock (_gate)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var account = EnsureAccount(db);
            var openPositions = db.Set<Domain.Entities.LedgerPosition>()
                .AsNoTracking()
                .Where(x => !x.IsClosed)
                .ToList();

            var exposure = GetExposurePercentUnsafe(account.CashAed, openPositions.Sum(x => x.DebitAed));
            return new LedgerStateContract(
                CashAed: decimal.Round(account.CashAed, 2),
                GoldGrams: decimal.Round(account.GoldGrams, 2),
                OpenExposurePercent: decimal.Round(exposure, 2),
                DeployableCashAed: decimal.Round(Math.Max(0m, account.CashAed), 2),
                OpenBuyCount: openPositions.Count);
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

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var account = EnsureAccount(db);

            if (account.CashAed <= 0m)
            {
                return false;
            }

            var openPositions = db.Set<Domain.Entities.LedgerPosition>()
                .AsNoTracking()
                .Where(x => !x.IsClosed)
                .OrderByDescending(x => x.Mt5BuyTime)
                .ToList();

            var exposure = GetExposurePercentUnsafe(account.CashAed, openPositions.Sum(x => x.DebitAed));
            if (exposure >= exposureCapPercent)
            {
                return false;
            }

            var latestEntry = openPositions.FirstOrDefault();
            if (latestEntry is null)
            {
                return true;
            }

            var spacing = Math.Abs(currentPrice - latestEntry.Mt5BuyPrice) / latestEntry.Mt5BuyPrice;
            return spacing >= minSpacingPercent;
        }
    }

    public TradeSlipContract ApplyBuyFill(Guid tradeId, decimal grams, decimal mt5BuyPrice, DateTimeOffset mt5Time)
    {
        lock (_gate)
        {
            var normalizedGrams = Math.Max(0m, grams);
            if (normalizedGrams < MinTradeGrams)
            {
                throw new InvalidOperationException("Buy fill rejected: grams below 100g minimum.");
            }

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var account = EnsureAccount(db);

            var existing = db.Set<Domain.Entities.LedgerPosition>().FirstOrDefault(x => x.TradeId == tradeId);
            if (existing is not null)
            {
                throw new InvalidOperationException("Buy fill rejected: duplicate tradeId in ledger.");
            }

            var shopBuy = mt5BuyPrice + 0.80m;
            var debit = ToAed(shopBuy, normalizedGrams);

            if (debit > account.CashAed)
            {
                throw new InvalidOperationException("Buy fill rejected: insufficient ledger cash.");
            }

            account.ApplyBuy(debit, normalizedGrams);
            var position = Domain.Entities.LedgerPosition.Open(tradeId, normalizedGrams, mt5BuyPrice, shopBuy, debit, mt5Time);
            db.Set<Domain.Entities.LedgerPosition>().Add(position);
            db.SaveChanges();

            var ksaTime = mt5Time.AddMinutes(50);
            return new TradeSlipContract(
                SlipType: "BUY",
                TradeId: tradeId,
                Grams: decimal.Round(normalizedGrams, 2),
                Mt5Price: decimal.Round(mt5BuyPrice, 2),
                ShopPrice: decimal.Round(shopBuy, 2),
                AmountAed: decimal.Round(debit, 2),
                NetProfitAed: 0m,
                CashBalanceAed: decimal.Round(account.CashAed, 2),
                GoldBalanceGrams: decimal.Round(account.GoldGrams, 2),
                Mt5Time: mt5Time,
                KsaTime: ksaTime,
                Message: $"BUY SLIP {tradeId} | {normalizedGrams:0.##}g @ {shopBuy:0.00} | Debit AED {debit:0.00}");
        }
    }

    public TradeSlipContract? ApplySellFill(Guid tradeId, decimal mt5SellPrice, DateTimeOffset mt5Time)
    {
        lock (_gate)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var account = EnsureAccount(db);

            var position = db.Set<Domain.Entities.LedgerPosition>()
                .FirstOrDefault(x => x.TradeId == tradeId);
            if (position is null || position.IsClosed)
            {
                return null;
            }

            var shopSell = mt5SellPrice - 0.80m;
            var credit = ToAed(shopSell, position.Grams);
            var netProfit = credit - position.DebitAed;

            account.ApplySell(credit, position.Grams);
            position.Close(mt5Time);
            db.SaveChanges();

            var ksaTime = mt5Time.AddMinutes(50);
            return new TradeSlipContract(
                SlipType: "SELL",
                TradeId: tradeId,
                Grams: decimal.Round(position.Grams, 2),
                Mt5Price: decimal.Round(mt5SellPrice, 2),
                ShopPrice: decimal.Round(shopSell, 2),
                AmountAed: decimal.Round(credit, 2),
                NetProfitAed: decimal.Round(netProfit, 2),
                CashBalanceAed: decimal.Round(account.CashAed, 2),
                GoldBalanceGrams: decimal.Round(account.GoldGrams, 2),
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
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var account = EnsureAccount(db);
            account.ApplyDeposit(amountAed);
            db.SaveChanges();
            return new TradeSlipContract(
                SlipType: "DEPOSIT",
                TradeId: Guid.NewGuid(),
                Grams: 0m,
                Mt5Price: 0m,
                ShopPrice: 0m,
                AmountAed: decimal.Round(amountAed, 2),
                NetProfitAed: 0m,
                CashBalanceAed: decimal.Round(account.CashAed, 2),
                GoldBalanceGrams: decimal.Round(account.GoldGrams, 2),
                Mt5Time: timestamp,
                KsaTime: timestamp.AddHours(3),
                Message: $"DEPOSIT | AED {amountAed:0.00} | {note} | Balance AED {account.CashAed:0.00}");
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
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var account = EnsureAccount(db);
            if (amountAed > account.CashAed)
            {
                throw new InvalidOperationException("Withdrawal exceeds available cash balance.");
            }

            account.ApplyWithdrawal(amountAed);
            db.SaveChanges();
            return new TradeSlipContract(
                SlipType: "WITHDRAWAL",
                TradeId: Guid.NewGuid(),
                Grams: 0m,
                Mt5Price: 0m,
                ShopPrice: 0m,
                AmountAed: decimal.Round(amountAed, 2),
                NetProfitAed: 0m,
                CashBalanceAed: decimal.Round(account.CashAed, 2),
                GoldBalanceGrams: decimal.Round(account.GoldGrams, 2),
                Mt5Time: timestamp,
                KsaTime: timestamp.AddHours(3),
                Message: $"WITHDRAWAL | AED {amountAed:0.00} | {note} | Balance AED {account.CashAed:0.00}");
        }
    }

    public TradeSlipContract ShopAdjustment(decimal adjustmentAed, string note, DateTimeOffset timestamp)
    {
        lock (_gate)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var account = EnsureAccount(db);
            account.ApplyAdjustment(adjustmentAed);
            db.SaveChanges();
            return new TradeSlipContract(
                SlipType: "ADJUSTMENT",
                TradeId: Guid.NewGuid(),
                Grams: 0m,
                Mt5Price: 0m,
                ShopPrice: 0m,
                AmountAed: decimal.Round(Math.Abs(adjustmentAed), 2),
                NetProfitAed: decimal.Round(adjustmentAed, 2),
                CashBalanceAed: decimal.Round(account.CashAed, 2),
                GoldBalanceGrams: decimal.Round(account.GoldGrams, 2),
                Mt5Time: timestamp,
                KsaTime: timestamp.AddHours(3),
                Message: $"SHOP_ADJUSTMENT | AED {adjustmentAed:+0.00;-0.00} | {note} | Balance AED {account.CashAed:0.00}");
        }
    }

    private static Domain.Entities.LedgerAccount EnsureAccount(ApplicationDbContext db)
    {
        var account = db.Set<Domain.Entities.LedgerAccount>().FirstOrDefault();
        if (account is not null)
        {
            return account;
        }

        account = Domain.Entities.LedgerAccount.CreateDefault();
        db.Set<Domain.Entities.LedgerAccount>().Add(account);
        db.SaveChanges();
        return account;
    }

    private static decimal GetExposurePercentUnsafe(decimal cashAed, decimal totalOpenCost)
    {
        var equity = cashAed + totalOpenCost;
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
}
