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
        string? goldRegime = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"symbol={snapshot.Symbol} bid={snapshot.Bid} ask={snapshot.Ask} spread={snapshot.Spread}");
        sb.AppendLine($"serverTime={snapshot.Mt5ServerTime:O} KSATime={snapshot.KsaTime:O} session={snapshot.Session} phase={snapshot.SessionPhase}");
        sb.AppendLine($"H4/H1/M15: close~{snapshot.AuthoritativeRate} MA20H1={snapshot.Ma20H1} RSI_H1={snapshot.RsiH1} RSI_M15={snapshot.RsiM15} ATR_M15={snapshot.AtrM15}");
        sb.AppendLine($"regime={regime} overextensionState={overextensionState} waterfallRisk={waterfallRisk} hazardWindowActive={hazardWindowActive}");
        sb.AppendLine($"S1={s1} S2={s2} R1={r1} R2={r2} FAIL={fail}");
        sb.AppendLine($"ADR_used={snapshot.AdrUsedPct}% compressionM15={snapshot.CompressionCountM15} expansionM15={snapshot.ExpansionCountM15}");
        sb.AppendLine($"candidateState={candidateState ?? "NONE"} candidateBase={candidateBase} candidateLid={candidateLid} candidatePath={candidatePath} candidateExpiry={candidateExpiry}");
        if (ledger != null)
            sb.AppendLine($"ledgerCashAED={ledger.CashAed} ledgerGoldGrams={ledger.GoldGrams} deployableAED={ledger.DeployableCashAed}");
        if (!string.IsNullOrEmpty(goldRegime))
            sb.AppendLine($"goldRegime={goldRegime}");
        return sb.ToString();
    }
}
