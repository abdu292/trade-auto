using Brain.Application.Common.Models;
using System.Text;

namespace Brain.Application.Common.Services;

/// <summary>
/// Spec v11 §19 — Structured text context packet for AI and logging.
/// Replaces screenshot dependence; ensures all AIs see the same deterministic snapshot.
/// </summary>
public static class StructuredContextPacketBuilder
{
    public static string Build(
        MarketSnapshotContract snapshot,
        string regime,
        string overextensionState,
        string waterfallRisk,
        bool hazardWindowActive,
        decimal? s1,
        decimal? s2,
        decimal? r1,
        decimal? r2,
        decimal? fail,
        string? candidateState,
        decimal? candidateBase,
        decimal? candidateLid,
        string? candidatePath,
        DateTimeOffset? candidateExpiry,
        LedgerStateContract? ledger,
        string? goldRegime = null,
        string? dxyState = null,
        string? silverCrossMetalState = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"symbol={snapshot.Symbol} bid={snapshot.Bid} ask={snapshot.Ask} spread={snapshot.Spread} rateNow={snapshot.Bid}");
        sb.AppendLine($"serverTime={snapshot.Mt5ServerTime:O} KSATime={snapshot.KsaTime:O} session={snapshot.Session} phase={snapshot.SessionPhase}");
        sb.AppendLine($"Complete indicators: RSI(14) H1={snapshot.RsiH1} M15={snapshot.RsiM15} M5={GetRsiM5(snapshot)} MA(20) H1={snapshot.Ma20H1} M15={snapshot.Ma20M30} M5={snapshot.Ma20M5} ATR_M15={snapshot.AtrM15} Volume in TimeframeData");
        sb.AppendLine($"H4/H1/M15: close~{snapshot.AuthoritativeRate} MA20H1={snapshot.Ma20H1} RSI_H1={snapshot.RsiH1} RSI_M15={snapshot.RsiM15} ATR_M15={snapshot.AtrM15}");
        sb.AppendLine($"regime={regime} overextensionState={overextensionState} waterfallRisk={waterfallRisk} hazardWindowActive={hazardWindowActive}");
        sb.AppendLine($"DXY={dxyState ?? "—"} Silver/cross-metal={silverCrossMetalState ?? "—"} (for validation)");
        sb.AppendLine($"S1={s1} S2={s2} R1={r1} R2={r2} FAIL={fail}");
        sb.AppendLine($"ADR_used={snapshot.AdrUsedPct}% compressionM15={snapshot.CompressionCountM15} expansionM15={snapshot.ExpansionCountM15}");
        sb.AppendLine($"candidateState={candidateState ?? "NONE"} candidateBase={candidateBase} candidateLid={candidateLid} candidatePath={candidatePath} candidateExpiry={candidateExpiry}");
        if (ledger != null)
            sb.AppendLine($"ledgerCashAED={ledger.CashAed} ledgerGoldGrams={ledger.GoldGrams} deployableAED={ledger.DeployableCashAed}");
        if (!string.IsNullOrEmpty(goldRegime))
            sb.AppendLine($"goldRegime={goldRegime}");
        return sb.ToString();
    }

    private static decimal GetRsiM5(MarketSnapshotContract snapshot)
    {
        var m5 = snapshot.TimeframeData?.FirstOrDefault(t => string.Equals(t.Timeframe, "M5", StringComparison.OrdinalIgnoreCase));
        return m5?.Rsi ?? 0m;
    }
}
