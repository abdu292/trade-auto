using Brain.Domain.Common;

namespace Brain.Domain.Entities;

public sealed class LedgerAccount : BaseEntity<Guid>
{
    private LedgerAccount()
    {
    }

    public decimal InitialCashAed { get; private set; }
    public decimal CashAed { get; private set; }
    public decimal GoldGrams { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static LedgerAccount CreateDefault(decimal initialCashAed = 100000m)
    {
        var normalized = initialCashAed <= 0m ? 100000m : initialCashAed;
        return new LedgerAccount
        {
            Id = Guid.NewGuid(),
            InitialCashAed = normalized,
            CashAed = normalized,
            GoldGrams = 0m,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    public void ApplyBuy(decimal debitAed, decimal grams)
    {
        CashAed -= debitAed;
        GoldGrams += grams;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void ApplySell(decimal creditAed, decimal grams)
    {
        CashAed += creditAed;
        GoldGrams -= grams;
        if (GoldGrams < 0m)
        {
            GoldGrams = 0m;
        }

        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
