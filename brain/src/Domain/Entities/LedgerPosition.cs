using Brain.Domain.Common;

namespace Brain.Domain.Entities;

public sealed class LedgerPosition : BaseEntity<Guid>
{
    private LedgerPosition()
    {
    }

    public Guid TradeId { get; private set; }
    public decimal Grams { get; private set; }
    public decimal Mt5BuyPrice { get; private set; }
    public decimal ShopBuyPrice { get; private set; }
    public decimal DebitAed { get; private set; }
    public DateTimeOffset Mt5BuyTime { get; private set; }
    public bool IsClosed { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public decimal Mt5SellPrice { get; private set; }
    public decimal ShopSellPrice { get; private set; }
    public decimal CreditAed { get; private set; }
    public decimal NetProfitAed { get; private set; }
    public string ClosedSession { get; private set; } = string.Empty;
    public string OpenedSession { get; private set; } = string.Empty;

    public static LedgerPosition Open(
        Guid tradeId,
        decimal grams,
        decimal mt5BuyPrice,
        decimal shopBuyPrice,
        decimal debitAed,
        DateTimeOffset mt5BuyTime,
        string openedSession = "")
    {
        return new LedgerPosition
        {
            Id = Guid.NewGuid(),
            TradeId = tradeId,
            Grams = grams,
            Mt5BuyPrice = mt5BuyPrice,
            ShopBuyPrice = shopBuyPrice,
            DebitAed = debitAed,
            Mt5BuyTime = mt5BuyTime,
            IsClosed = false,
            ClosedAtUtc = null,
            OpenedSession = openedSession,
        };
    }

    public void Close(
        DateTimeOffset closedAtUtc,
        decimal mt5SellPrice = 0m,
        decimal shopSellPrice = 0m,
        decimal creditAed = 0m,
        decimal netProfitAed = 0m,
        string closedSession = "")
    {
        IsClosed = true;
        ClosedAtUtc = closedAtUtc;
        Mt5SellPrice = mt5SellPrice;
        ShopSellPrice = shopSellPrice;
        CreditAed = creditAed;
        NetProfitAed = netProfitAed;
        ClosedSession = closedSession;
    }
}
