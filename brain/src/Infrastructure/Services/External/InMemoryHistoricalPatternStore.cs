using Brain.Application.Common.Interfaces;
using Brain.Application.Common.Models;
using Brain.Application.Common.Services;
using Microsoft.Extensions.Logging;

namespace Brain.Infrastructure.Services.External;

/// <summary>
/// In-memory implementation of IHistoricalPatternStore for development/testing.
/// In production, this would query a 10+ year historical database.
/// </summary>
public sealed class InMemoryHistoricalPatternStore(
    ILogger<InMemoryHistoricalPatternStore> logger) : IHistoricalPatternStore
{
    private static readonly List<HistoricalPatternMatch> _patterns = [];
    private static readonly Lock _gate = new();

    public Task<IReadOnlyCollection<HistoricalPatternMatch>> FindMatchesAsync(
        MarketSnapshotContract snapshot,
        string session,
        string phase,
        DayOfWeek dayOfWeek,
        CancellationToken cancellationToken = default)
    {
        // In production, this would:
        // 1. Query historical database for similar market conditions
        // 2. Match by: session, phase, day of week, regime, volatility state
        // 3. Return patterns with continuation scores, reversal risks, etc.

        lock (_gate)
        {
            // For now, return empty or generate synthetic matches based on current conditions
            var matches = _patterns
                .Where(p => p.Session == session || p.Session == "ANY")
                .Where(p => p.RegimeTag == snapshot.Session || p.RegimeTag == "ANY")
                .Take(10)
                .ToList();

            return Task.FromResult<IReadOnlyCollection<HistoricalPatternMatch>>(matches);
        }
    }

    /// <summary>
    /// Adds a pattern match to the store (for testing/development).
    /// </summary>
    public static void AddPattern(HistoricalPatternMatch pattern)
    {
        lock (_gate)
        {
            _patterns.Add(pattern);
            // Keep only last 1000 patterns
            if (_patterns.Count > 1000)
            {
                _patterns.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Clears all patterns (for testing/development).
    /// </summary>
    public static void Clear()
    {
        lock (_gate)
        {
            _patterns.Clear();
        }
    }
}