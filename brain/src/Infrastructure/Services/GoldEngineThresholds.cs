using Brain.Application.Common.Interfaces;

namespace Brain.Infrastructure.Services;

/// <summary>Spec v7 §6 default thresholds. Can be overridden via IConfiguration.</summary>
public sealed class GoldEngineThresholds : IGoldEngineThresholds
{
    public decimal Ma20DistNormalMax => 0.8m;
    public decimal Ma20DistStretchedMax => 1.5m;

    public decimal RsiLowBound => 35m;
    public decimal RsiMidLow => 35m;
    public decimal RsiMidHigh => 65m;
    public decimal RsiHighBound => 75m;
    public decimal RsiExtremeBound => 75m;
    public decimal RsiBuyLimitCautionHigh => 72m;
    public decimal RsiBuyLimitWaitHigh => 75m;

    public decimal BaseDistAtrBuyLimitValidMax => 1.0m;
    public decimal BaseDistAtrBuyLimitRearmMax => 0.4m;

    public decimal AdrUsedFullBound => 0.9m;
    public decimal AdrUsedBlockContinuationBuyStopMin => 1.0m;

    public decimal VciCompressedMax => 0.7m;
    public decimal VciNormalMax => 1.3m;

    public decimal SpreadCaution => 0.5m;
    public decimal SpreadBlock => 0.7m;
    public decimal TpDistanceSpreadMinRatio => 3m;

    public decimal SessionSizeJapan => 0.5m;
    public decimal SessionSizeIndia => 0.7m;
    public decimal SessionSizeLondon => 1.0m;
    public decimal SessionSizeNy => 0.6m;

    public (int Min, int Max) ExpiryJapan => (90, 120);
    public (int Min, int Max) ExpiryIndia => (90, 150);
    public (int Min, int Max) ExpiryLondon => (60, 90);
    public (int Min, int Max) ExpiryNy => (45, 60);

    public int ConfidenceWaitMax => 59;
    public int ConfidenceMicroMin => 60;
    public int ConfidenceMicroMax => 74;
    public int ConfidenceNormalMin => 75;
    public int ConfidenceHighMin => 90;
}
