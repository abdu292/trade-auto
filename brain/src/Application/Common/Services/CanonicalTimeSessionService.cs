namespace Brain.Application.Common.Services;

/// <summary>
/// PATCH 1 — TIME INTEGRITY LAYER
/// Single canonical time/session authority per client audit.
/// All downstream modules must consume only this output.
/// Emits TIME_MAPPING_CONFLICT when clock and session engine disagree.
/// When MT5 server time yields OFFHOURS/LATE_NY (e.g. replay or wrong server time),
/// falls back to deriving server time from snapshot KSA or India time so Asia session is correct.
/// </summary>
public static class CanonicalTimeSessionService
{
    /// <summary>Canonical source name: SessionEngine (Server Time as base per spec).</summary>
    public const string CanonicalSource = "SessionEngine";

    /// <summary>
    /// Resolves canonical session and phase from MT5 server time.
    /// When optional snapshot KSA/India times are provided and primary resolution is OFFHOURS or LATE_NY,
    /// derives effective server time from KSA (or India) and re-resolves so Asia session is correct (e.g. replay).
    /// </summary>
    public static CanonicalTimeSessionResult Resolve(
        DateTimeOffset mt5ServerTime,
        int mt5ToKsaOffsetMinutes = 50,
        DateTimeOffset? snapshotKsaTime = null,
        DateTimeOffset? snapshotIndiaTime = null,
        DateTimeOffset? snapshotUaeTime = null)
    {
        var engineResult = SessionEngine.Resolve(mt5ServerTime);
        var clockSession = TradingSessionClock.Resolve(mt5ServerTime);
        var ksaTime = TradingSessionClock.ServerTimeToKsa(mt5ServerTime);
        var istTime = TradingSessionClock.ServerTimeToIst(mt5ServerTime);
        bool usedKsaOrIndiaFallback = false;
        string? fallbackSource = null;

        // Replay / wrong server time: if primary is OFFHOURS or LATE_NY but snapshot has valid KSA/India/UAE that clearly falls in a session, derive server time and re-resolve
        if ((engineResult.Session == "OFFHOURS" || engineResult.Session == "LATE_NY") &&
            (HasValidTime(snapshotKsaTime) || HasValidTime(snapshotIndiaTime) || HasValidTime(snapshotUaeTime)))
        {
            DateTimeOffset? derivedServer = null;
            if (HasValidTime(snapshotKsaTime))
            {
                derivedServer = snapshotKsaTime!.Value.AddMinutes(-mt5ToKsaOffsetMinutes);
                fallbackSource = "KsaTime";
            }
            else if (HasValidTime(snapshotIndiaTime))
            {
                derivedServer = snapshotIndiaTime!.Value.AddHours(-3).AddMinutes(-20); // Server = IST - 3h20m
                fallbackSource = "IndiaTime";
            }
            else if (HasValidTime(snapshotUaeTime))
            {
                // UAE/Dubai = UTC+4, KSA = UTC+3 → KSA = UAE - 1h. Server = KSA - 50min = UAE - 1h50m
                derivedServer = snapshotUaeTime!.Value.AddHours(-1).AddMinutes(-50);
                fallbackSource = "UaeTime";
            }

            if (derivedServer.HasValue)
            {
                var fallbackResult = SessionEngine.Resolve(derivedServer.Value);
                if (fallbackResult.Session is "JAPAN" or "INDIA" or "LONDON" or "NEW_YORK" or "TRANSITION")
                {
                    engineResult = fallbackResult;
                    ksaTime = TradingSessionClock.ServerTimeToKsa(derivedServer.Value);
                    istTime = TradingSessionClock.ServerTimeToIst(derivedServer.Value);
                    usedKsaOrIndiaFallback = true;
                }
            }
        }

        var conflict = !SessionPhaseEquivalent(clockSession.Session, clockSession.Phase, engineResult.Session, engineResult.Phase);
        string? conflictDetail = null;
        if (conflict)
        {
            conflictDetail = $"Clock=({clockSession.Session},{clockSession.Phase}) Engine=({engineResult.Session},{engineResult.Phase})";
        }
        if (usedKsaOrIndiaFallback)
        {
            conflictDetail = (conflictDetail ?? "") + (string.IsNullOrEmpty(conflictDetail) ? "" : "; ") + $"Session derived from {fallbackSource} (MT5 server time likely wrong/replay).";
        }

        return new CanonicalTimeSessionResult(
            CanonicalSession: engineResult.Session,
            CanonicalPhase: engineResult.Phase,
            SessionEngineResult: engineResult,
            Mt5ServerTime: mt5ServerTime,
            KsaTime: ksaTime,
            IstTime: istTime,
            DerivedKsaOffsetMinutes: mt5ToKsaOffsetMinutes,
            HasConflict: conflict || usedKsaOrIndiaFallback,
            ConflictDetail: conflictDetail,
            ConfidenceDegraded: conflict,
            UsedKsaOrIndiaFallback: usedKsaOrIndiaFallback,
            FallbackSource: fallbackSource);
    }

    /// <summary>True if the value is set and not default (year &gt; 1 and reasonable date).</summary>
    private static bool HasValidTime(DateTimeOffset? t)
    {
        if (!t.HasValue) return false;
        var v = t.Value;
        if (v.Year < 2000 || v.Year > 2100) return false;
        return true;
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
    bool ConfidenceDegraded,
    bool UsedKsaOrIndiaFallback = false,
    string? FallbackSource = null);
