using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// Dynamic Session Risk Service.
/// Provides session-adaptive size modifiers that adapt from real trade results.
///
/// Bootstrap modifiers (initial values):
///   Japan    → 0.60
///   India    → 0.60
///   London   → 0.65
///   New York → 0.55
///
/// Hard bounds:  minimum 0.45 — maximum 0.80
///
/// Waterfall cap: if a session records ≥3 waterfall entries in the last 50 trades,
/// maxModifier for that session is temporarily capped at 0.60 until the next update.
///
/// This service only adjusts CAUTION sizing. It must never change legality.
/// </summary>
public sealed class DynamicSessionRiskService
{
    // Bootstrap modifiers
    private const decimal JapanBootstrap  = 0.60m;
    private const decimal IndiaBootstrap  = 0.60m;
    private const decimal LondonBootstrap = 0.65m;
    private const decimal NyBootstrap     = 0.55m;

    // Hard bounds
    private const decimal MinModifier          = 0.45m;
    private const decimal MaxModifier          = 0.80m;
    private const decimal WaterfallCappedMax   = 0.60m;
    private const int     WaterfallThreshold   = 3;
    private const int     LookbackTradeCount   = 50;

    // In-memory session state: modifier + sliding window of last N trade outcomes (true = waterfall)
    private readonly Lock _lock = new();
    private readonly Dictionary<string, decimal> _modifiers;
    // Sliding window queue per session: true = waterfall, false = success
    private readonly Dictionary<string, Queue<bool>> _tradeHistories;

    public DynamicSessionRiskService()
    {
        _modifiers = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["JAPAN"]  = JapanBootstrap,
            ["INDIA"]  = IndiaBootstrap,
            ["LONDON"] = LondonBootstrap,
            ["NY"]     = NyBootstrap,
        };

        _tradeHistories = new Dictionary<string, Queue<bool>>(StringComparer.OrdinalIgnoreCase)
        {
            ["JAPAN"]  = new Queue<bool>(),
            ["INDIA"]  = new Queue<bool>(),
            ["LONDON"] = new Queue<bool>(),
            ["NY"]     = new Queue<bool>(),
        };
    }

    /// <summary>
    /// Returns the current dynamic size modifier for the given session.
    /// The waterfall count is derived from the last LookbackTradeCount trades in the sliding window.
    /// </summary>
    public DynamicSessionRiskResult GetModifier(string session)
    {
        var normalized = NormalizeSession(session);

        lock (_lock)
        {
            var modifier = _modifiers.TryGetValue(normalized, out var m)
                ? m
                : 0.60m; // default fallback

            var waterfallCount = CountWaterfallsInWindow(normalized);
            var waterfallCapActive = waterfallCount >= WaterfallThreshold;

            if (waterfallCapActive && modifier > WaterfallCappedMax)
            {
                modifier = WaterfallCappedMax;
            }

            return new DynamicSessionRiskResult(
                Session: normalized,
                Modifier: modifier,
                WaterfallCapActive: waterfallCapActive,
                Reason: waterfallCapActive
                    ? $"Session {normalized}: modifier={modifier:0.00} (waterfall cap active, {waterfallCount}/{WaterfallThreshold} waterfalls in last {LookbackTradeCount} trades)"
                    : $"Session {normalized}: modifier={modifier:0.00} (normal, {waterfallCount} waterfalls in last {LookbackTradeCount} trades)");
        }
    }

    /// <summary>
    /// Records a waterfall entry for the session in the sliding window.
    /// Called when a trade results in a waterfall catch.
    /// </summary>
    public void RecordWaterfallEntry(string session)
    {
        var normalized = NormalizeSession(session);

        lock (_lock)
        {
            EnqueueOutcome(normalized, isWaterfall: true);
        }
    }

    /// <summary>
    /// Records a successful (non-waterfall) trade for the session in the sliding window.
    /// </summary>
    public void RecordSuccessfulTrade(string session)
    {
        var normalized = NormalizeSession(session);

        lock (_lock)
        {
            EnqueueOutcome(normalized, isWaterfall: false);
        }
    }

    /// <summary>
    /// Updates the modifier for a session from external study/performance analysis.
    /// Enforces hard bounds [MinModifier, MaxModifier].
    /// </summary>
    public void UpdateModifier(string session, decimal newModifier)
    {
        var normalized = NormalizeSession(session);
        var clamped = Math.Clamp(newModifier, MinModifier, MaxModifier);

        lock (_lock)
        {
            _modifiers[normalized] = clamped;
        }
    }

    /// <summary>Returns a snapshot of all current session modifiers (for timeline logging).</summary>
    public IReadOnlyDictionary<string, decimal> GetAllModifiers()
    {
        lock (_lock)
        {
            return new Dictionary<string, decimal>(_modifiers, StringComparer.OrdinalIgnoreCase);
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private void EnqueueOutcome(string normalizedSession, bool isWaterfall)
    {
        if (!_tradeHistories.TryGetValue(normalizedSession, out var queue))
        {
            queue = new Queue<bool>();
            _tradeHistories[normalizedSession] = queue;
        }

        queue.Enqueue(isWaterfall);

        // Maintain sliding window size: discard oldest entry when full
        while (queue.Count > LookbackTradeCount)
            queue.Dequeue();
    }

    private int CountWaterfallsInWindow(string normalizedSession)
    {
        if (!_tradeHistories.TryGetValue(normalizedSession, out var queue))
            return 0;

        return queue.Count(isWaterfall => isWaterfall);
    }

    private static string NormalizeSession(string session)
    {
        var s = (session ?? string.Empty).Trim().ToUpperInvariant();
        return s switch
        {
            "ASIA"     => "JAPAN",
            "EUROPE"   => "LONDON",
            "NEW_YORK" => "NY",
            _          => s,
        };
    }
}
