namespace Brain.Application.Common.Interfaces;

/// <summary>
/// Stores expected entry price per trade ID so execution quality (slippage) can be logged when MT5 reports fill.
/// </summary>
public interface IExpectedEntryStore
{
    void Set(Guid tradeId, decimal expectedEntryPrice);
    bool TryGet(Guid tradeId, out decimal expectedEntryPrice);
}
