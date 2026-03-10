namespace Brain.Application.Common.Interfaces;

/// <summary>Spec v7 §6 — configurable thresholds. Must be config values, not hardcoded.</summary>
public interface IGoldEngineThresholds
{
    // §6.1 MA20 distance (normalized by ATR)
    decimal Ma20DistNormalMax { get; }
    decimal Ma20DistStretchedMax { get; }

    // §6.2 RSI
    decimal RsiLowBound { get; }
    decimal RsiMidLow { get; }
    decimal RsiMidHigh { get; }
    decimal RsiHighBound { get; }
    decimal RsiExtremeBound { get; }
    decimal RsiBuyLimitCautionHigh { get; }
    decimal RsiBuyLimitWaitHigh { get; }

    // §6.3 Base distance ATR
    decimal BaseDistAtrBuyLimitValidMax { get; }
    decimal BaseDistAtrBuyLimitRearmMax { get; }

    // §6.4 ADR
    decimal AdrUsedFullBound { get; }
    decimal AdrUsedBlockContinuationBuyStopMin { get; }

    // §6.5 VCI
    decimal VciCompressedMax { get; }
    decimal VciNormalMax { get; }

    // §6.6 Spread
    decimal SpreadCaution { get; }
    decimal SpreadBlock { get; }
    decimal TpDistanceSpreadMinRatio { get; }

    // §6.7 Session size multipliers
    decimal SessionSizeJapan { get; }
    decimal SessionSizeIndia { get; }
    decimal SessionSizeLondon { get; }
    decimal SessionSizeNy { get; }

    // §6.8 Expiry (minutes)
    (int Min, int Max) ExpiryJapan { get; }
    (int Min, int Max) ExpiryIndia { get; }
    (int Min, int Max) ExpiryLondon { get; }
    (int Min, int Max) ExpiryNy { get; }

    // §7 Confidence
    int ConfidenceWaitMax { get; }
    int ConfidenceMicroMin { get; }
    int ConfidenceMicroMax { get; }
    int ConfidenceNormalMin { get; }
    int ConfidenceHighMin { get; }
}
