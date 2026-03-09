using Brain.Application.Common.Models;

namespace Brain.Application.Common.Services;

/// <summary>
/// CR11 — Dynamic Session Risk Service.
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

    // In-memory session state (modifier + waterfall counter)
    private readonly Lock _lock = new();
    private readonly Dictionary<string, decimal> _modifiers;
    private readonly Dictionary<string, int> _waterfallCounts;

    public DynamicSessionRiskService()
    {
        _modifiers = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["JAPAN"]  = JapanBootstrap,
            ["INDIA"]  = IndiaBootstrap,
            ["LONDON"] = LondonBootstrap,
            ["NY"]     = NyBootstrap,
        };

        _waterfallCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["JAPAN"]  = 0,
            ["INDIA"]  = 0,
            ["LONDON"] = 0,
            ["NY"]     = 0,
        };
    }

    /// <summary>
    /// Returns the current dynamic size modifier for the given session.
    /// </summary>
    public DynamicSessionRiskResult GetModifier(string session)
    {
        var normalized = NormalizeSession(session);

        lock (_lock)
        {
            var modifier = _modifiers.TryGetValue(normalized, out var m)
                ? m
                : 0.60m; // default fallback

            var waterfallCount = _waterfallCounts.TryGetValue(normalized, out var w) ? w : 0;
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
                    ? $"Session {normalized}: modifier={modifier:0.00} (waterfall cap active, {waterfallCount}/{WaterfallThreshold} entries in last {LookbackTradeCount} trades)"
                    : $"Session {normalized}: modifier={modifier:0.00} (normal)");
        }
    }

    /// <summary>
    /// Records a waterfall entry for the session.
    /// Called when a trade results in a waterfall catch.
    /// </summary>
    public void RecordWaterfallEntry(string session)
    {
        var normalized = NormalizeSession(session);

        lock (_lock)
        {
            if (_waterfallCounts.ContainsKey(normalized))
                _waterfallCounts[normalized] = Math.Min(_waterfallCounts[normalized] + 1, LookbackTradeCount);
        }
    }

    /// <summary>
    /// Records a successful (non-waterfall) trade for the session.
    /// Slightly decrements the waterfall counter to allow recovery over time.
    /// </summary>
    public void RecordSuccessfulTrade(string session)
    {
        var normalized = NormalizeSession(session);

        lock (_lock)
        {
            if (_waterfallCounts.ContainsKey(normalized) && _waterfallCounts[normalized] > 0)
                _waterfallCounts[normalized]--;
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
