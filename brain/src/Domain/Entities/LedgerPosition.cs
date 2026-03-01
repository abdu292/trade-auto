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

    public static LedgerPosition Open(
        Guid tradeId,
        decimal grams,
        decimal mt5BuyPrice,
        decimal shopBuyPrice,
        decimal debitAed,
        DateTimeOffset mt5BuyTime)
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
        };
    }

    public void Close(DateTimeOffset closedAtUtc)
    {
        IsClosed = true;
        ClosedAtUtc = closedAtUtc;
    }
}
