namespace Brain.Application.Common.Services;

/// <summary>
/// Implements the CR10 Capital Utilization formula block.
/// Validates and resizes gold buy orders against available cash before MT5 execution.
/// This gate is immutable — MT5 must only accept orders approved by this check.
/// </summary>
public static class CapitalUtilizationService
{
    // ── Constants (CR10) ────────────────────────────────────────────────────────
    private const decimal UsdToAed = 3.674m;
    private const decimal GramsPerOunce = 31.1035m;
    private const decimal ShopBuySpread = 0.80m;     // Shop buy spread per oz over MT5 price
    private const decimal CapitalBuffer = 0.995m;    // 99.5% of cash is deployable (0.5% safety reserve)

    /// <summary>
    /// CR10 — validates an order against available cash and MT5 buy price.
    /// Returns APPROVED, RESIZE_REQUIRED (with ApprovedGrams), or REJECTED.
    /// </summary>
    /// <param name="cashAed">Current liquid cash balance in AED.</param>
    /// <param name="mt5BuyPriceUsd">Current MT5 buy price in USD per oz (ask/authoritative rate).</param>
    /// <param name="orderGrams">The proposed trade size in grams.</param>
    public static CapitalUtilizationResult Check(decimal cashAed, decimal mt5BuyPriceUsd, decimal orderGrams)
    {
        // STEP 1: Real shop buy price (MT5 price + spread)
        var shopBuyUsd = mt5BuyPriceUsd + ShopBuySpread;

        // STEP 2: Cost per gram in AED
        var aedPerOunce = shopBuyUsd * UsdToAed;
        var aedPerGram = aedPerOunce / GramsPerOunce;

        // STEP 3: Safe available capital (99.5% of cash)
        var allowedCapitalAed = cashAed * CapitalBuffer;

        // STEP 4: Maximum legal grams (floor to 2 decimals for precision safety)
        var maxLegalGrams = aedPerGram > 0m
            ? Math.Floor((allowedCapitalAed / aedPerGram) * 100m) / 100m
            : 0m;

        // STEP 5: Order validation
        var requiredAed = orderGrams * aedPerGram;

        string orderStatus;
        decimal approvedGrams;

        if (maxLegalGrams <= 0m)
        {
            orderStatus = "REJECTED";
            approvedGrams = 0m;
        }
        else if (requiredAed <= allowedCapitalAed)
        {
            orderStatus = "APPROVED";
            approvedGrams = orderGrams;
        }
        else
        {
            orderStatus = "RESIZE_REQUIRED";
            approvedGrams = maxLegalGrams;
        }

        return new CapitalUtilizationResult(
            OrderStatus: orderStatus,
            ApprovedByCapacityGate: orderStatus != "REJECTED",
            ApprovedGrams: approvedGrams,
            MaxLegalGrams: maxLegalGrams,
            RequiredAed: decimal.Round(requiredAed, 4),
            AllowedCapitalAed: decimal.Round(allowedCapitalAed, 4),
            AedPerGram: decimal.Round(aedPerGram, 4),
            ShopBuyUsd: decimal.Round(shopBuyUsd, 2),
            CashAed: cashAed,
            Mt5BuyPriceUsd: mt5BuyPriceUsd,
            AttemptedGrams: orderGrams);
    }
}

/// <summary>
/// Result of the CR10 capital utilization check.
/// </summary>
/// <param name="OrderStatus">APPROVED | RESIZE_REQUIRED | REJECTED</param>
/// <param name="ApprovedByCapacityGate">True when MT5 execution is permitted (status is not REJECTED).</param>
/// <param name="ApprovedGrams">Final gram quantity to execute (same as attempted if APPROVED; MaxLegalGrams if RESIZE_REQUIRED; 0 if REJECTED).</param>
/// <param name="MaxLegalGrams">Maximum grams the current cash balance can support at this price.</param>
/// <param name="RequiredAed">AED cost of the attempted order.</param>
/// <param name="AllowedCapitalAed">99.5% of cash balance — the deployable ceiling.</param>
/// <param name="AedPerGram">Current cost per gram in AED (shop buy price converted).</param>
/// <param name="ShopBuyUsd">MT5 buy price + 0.80 spread per oz.</param>
/// <param name="CashAed">Cash balance input used for this check.</param>
/// <param name="Mt5BuyPriceUsd">MT5 authoritative rate input used for this check.</param>
/// <param name="AttemptedGrams">The originally proposed gram quantity before gate evaluation.</param>
public sealed record CapitalUtilizationResult(
    string OrderStatus,
    bool ApprovedByCapacityGate,
    decimal ApprovedGrams,
    decimal MaxLegalGrams,
    decimal RequiredAed,
    decimal AllowedCapitalAed,
    decimal AedPerGram,
    decimal ShopBuyUsd,
    decimal CashAed,
    decimal Mt5BuyPriceUsd,
    decimal AttemptedGrams);
