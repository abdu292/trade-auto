using Brain.Application.Common.Models;

namespace Brain.Application.Common.Interfaces;

public interface ITradeLedgerService
{
    LedgerStateContract GetState();
    bool CanScaleIn(decimal currentPrice, RegimeClassificationContract regime, decimal minSpacingPercent, decimal exposureCapPercent);
    TradeSlipContract ApplyBuyFill(Guid tradeId, decimal grams, decimal mt5BuyPrice, DateTimeOffset mt5Time);
    TradeSlipContract? ApplySellFill(Guid tradeId, decimal mt5SellPrice, DateTimeOffset mt5Time);
}
