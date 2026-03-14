namespace Brain.Application.Common.Services;

/// <summary>
/// PATCH 1 — TIME INTEGRITY LAYER
/// Single canonical time/session authority per client audit.
/// All downstream modules must consume only this output.
/// Emits TIME_MAPPING_CONFLICT when clock and session engine disagree.
/// </summary>
public static class CanonicalTimeSessionService
{
    /// <summary>Canonical source name: SessionEngine (Server Time as base per spec).</summary>
    public const string CanonicalSource = "SessionEngine";

    /// <summary>
    /// Resolves canonical session and phase from MT5 server time.
    /// Compares TradingSessionClock vs SessionEngine; if they disagree, conflict is set.
    /// </summary>
    public static CanonicalTimeSessionResult Resolve(DateTimeOffset mt5ServerTime, int mt5ToKsaOffsetMinutes = 50)
    {
        var clockSession = TradingSessionClock.Resolve(mt5ServerTime);
        var engineResult = SessionEngine.Resolve(mt5ServerTime);
        var ksaTime = TradingSessionClock.ServerTimeToKsa(mt5ServerTime);
        var istTime = TradingSessionClock.ServerTimeToIst(mt5ServerTime);

        var conflict = !SessionPhaseEquivalent(clockSession.Session, clockSession.Phase, engineResult.Session, engineResult.Phase);
        string? conflictDetail = null;
        if (conflict)
        {
            conflictDetail = $"Clock=({clockSession.Session},{clockSession.Phase}) Engine=({engineResult.Session},{engineResult.Phase})";
        }

        return new CanonicalTimeSessionResult(
            CanonicalSession: engineResult.Session,
            CanonicalPhase: engineResult.Phase,
            SessionEngineResult: engineResult,
            Mt5ServerTime: mt5ServerTime,
            KsaTime: ksaTime,
            IstTime: istTime,
            DerivedKsaOffsetMinutes: mt5ToKsaOffsetMinutes,
            HasConflict: conflict,
            ConflictDetail: conflictDetail,
            ConfidenceDegraded: conflict);
    }

    private static bool SessionPhaseEquivalent(string sessionA, string phaseA, string sessionB, string phaseB)
    {
        var s1 = (sessionA ?? "").Trim().ToUpperInvariant();
        var s2 = (sessionB ?? "").Trim().ToUpperInvariant();
        if (s1 != s2) return false;
        // Map phase names: OPEN/EARLY/MID/LATE/END vs START/MID/END/TRANSITION
        var p1 = NormalizePhase(phaseA);
        var p2 = NormalizePhase(phaseB);
        return p1 == p2;
    }

    private static string NormalizePhase(string phase)
    {
        var p = (phase ?? "").Trim().ToUpperInvariant();
        if (p == "OPEN" || p == "START" || p == "EARLY") return "START";
        if (p == "MID") return "MID";
        if (p == "LATE" || p == "END") return "END";
        if (p == "TRANSITION" || p.StartsWith("JAPAN_TO") || p.StartsWith("INDIA_TO") || p.StartsWith("LONDON_TO")) return "TRANSITION";
        return p;
    }
}

/// <summary>Output of canonical time/session resolution.</summary>
public sealed record CanonicalTimeSessionResult(
    string CanonicalSession,
    string CanonicalPhase,
    SessionEngineResult SessionEngineResult,
    DateTimeOffset Mt5ServerTime,
    DateTimeOffset KsaTime,
    DateTimeOffset IstTime,
    int DerivedKsaOffsetMinutes,
    bool HasConflict,
    string? ConflictDetail,
    bool ConfidenceDegraded);
