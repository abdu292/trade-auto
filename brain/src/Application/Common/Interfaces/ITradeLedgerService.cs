using Brain.Application.Common.Models;

namespace Brain.Application.Common.Interfaces;

public interface ITradeLedgerService
{
    LedgerStateContract GetState();
    LedgerStateContract GetExtendedState(decimal currentBidPrice);
    bool CanScaleIn(decimal currentPrice, RegimeClassificationContract regime, decimal minSpacingPercent, decimal exposureCapPercent);
    TradeSlipContract ApplyBuyFill(Guid tradeId, decimal grams, decimal mt5BuyPrice, DateTimeOffset mt5Time, string openedSession = "");
    TradeSlipContract? ApplySellFill(Guid tradeId, decimal mt5SellPrice, DateTimeOffset mt5Time, string closedSession = "");
    /// <summary>Add capital (deposit) to ledger cash.</summary>
    TradeSlipContract AddCapital(decimal amountAed, string note, DateTimeOffset timestamp);
    /// <summary>Withdraw capital from ledger cash.</summary>
    TradeSlipContract WithdrawCapital(decimal amountAed, string note, DateTimeOffset timestamp);
    /// <summary>Apply a shop price adjustment (revalue gold grams without cash flow).</summary>
    TradeSlipContract ShopAdjustment(decimal adjustmentAed, string note, DateTimeOffset timestamp);
}
